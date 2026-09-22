import { CfnOutput, Duration, RemovalPolicy, Stack, StackProps, Tags } from 'aws-cdk-lib';
import * as apigateway from 'aws-cdk-lib/aws-apigateway';
import * as dynamodb from 'aws-cdk-lib/aws-dynamodb';
import * as iam from 'aws-cdk-lib/aws-iam';
import { SqsEventSource } from 'aws-cdk-lib/aws-lambda-event-sources';
import * as s3 from 'aws-cdk-lib/aws-s3';
import * as s3n from 'aws-cdk-lib/aws-s3-notifications';
import * as sqs from 'aws-cdk-lib/aws-sqs';
import { Construct } from 'constructs';
import { DotNetFunction } from './dotnet-function';

/** S3 prefixes. These are also declared in C# (StorageKeys) and must stay in step. */
const RAW_PREFIX = 'pos/';
const ATTACHMENT_PREFIX = 'attachment/';
const RESULT_PREFIX = 'results/';

/** DynamoDB key attribute names, shared by both tables. */
const PARTITION_KEY = 'flightId';
const SORT_KEY = 'timestamp';

/** How long the calculator may run. The queue's visibility timeout is derived from it. */
const CALCULATOR_TIMEOUT = Duration.seconds(30);

/** Deliveries of a message before SQS gives up on it and moves it to the dead-letter queue. */
const MAX_RECEIVE_COUNT = 3;

/**
 * POS report processing pipeline.
 *
 *   POST /pos-reports -> ingest Lambda -> s3://bucket/pos/
 *                                            |  (object created)
 *                                            v
 *                        parser Lambda -> s3://bucket/attachment/ + parsed-reports table
 *                                            |  (queue message)
 *                                            v
 *                    calculator Lambda -> s3://bucket/results/ + results table
 *
 *   GET /status/{flightId} -> status Lambda -> both tables + s3://bucket/results/
 */
export class PosReportPipelineStack extends Stack {
  constructor(scope: Construct, id: string, props?: StackProps) {
    super(scope, id, props);

    Tags.of(this).add('project', 'pos-report-pipeline');

    // ---------------------------------------------------------------- storage

    const reportsBucket = new s3.Bucket(this, 'ReportsBucket', {
      encryption: s3.BucketEncryption.S3_MANAGED,
      blockPublicAccess: s3.BlockPublicAccess.BLOCK_ALL,
      enforceSSL: true,
      // A demo stack, so `cdk destroy` should leave nothing behind. A production
      // deployment would retain the bucket and the tables.
      removalPolicy: RemovalPolicy.DESTROY,
      autoDeleteObjects: true,
    });

    /** Structured POS reports, keyed by flight and the report's own timestamp. */
    const parsedReportsTable = new dynamodb.Table(this, 'ParsedReportsTable', {
      partitionKey: { name: PARTITION_KEY, type: dynamodb.AttributeType.STRING },
      sortKey: { name: SORT_KEY, type: dynamodb.AttributeType.STRING },
      billingMode: dynamodb.BillingMode.PAY_PER_REQUEST,
      removalPolicy: RemovalPolicy.DESTROY,
    });

    /** Calculation results, keyed by flight and the time the result was produced. */
    const resultsTable = new dynamodb.Table(this, 'ResultsTable', {
      partitionKey: { name: PARTITION_KEY, type: dynamodb.AttributeType.STRING },
      sortKey: { name: SORT_KEY, type: dynamodb.AttributeType.STRING },
      billingMode: dynamodb.BillingMode.PAY_PER_REQUEST,
      removalPolicy: RemovalPolicy.DESTROY,
    });

    // ---------------------------------------------------------------- queues

    /** Messages the calculator could not process after MAX_RECEIVE_COUNT deliveries. */
    const calculationDeadLetterQueue = new sqs.Queue(this, 'CalculationDeadLetterQueue', {
      retentionPeriod: Duration.days(14),
      enforceSSL: true,
    });

    const calculationQueue = new sqs.Queue(this, 'CalculationQueue', {
      // The consumer must have the full timeout to finish before a message becomes visible
      // again, otherwise the same report is calculated twice.
      visibilityTimeout: Duration.seconds(CALCULATOR_TIMEOUT.toSeconds() * 6),
      retentionPeriod: Duration.days(4),
      enforceSSL: true,
      deadLetterQueue: {
        queue: calculationDeadLetterQueue,
        maxReceiveCount: MAX_RECEIVE_COUNT,
      },
    });

    /**
     * S3 invokes the parser asynchronously, so there is no queue in front of it to fall back
     * on: this is where an event goes after Lambda has exhausted its own retries.
     */
    const parserDeadLetterQueue = new sqs.Queue(this, 'ParserDeadLetterQueue', {
      retentionPeriod: Duration.days(14),
      enforceSSL: true,
    });

    // ---------------------------------------------------------------- lambdas

    const ingestFunction = new DotNetFunction(this, 'IngestApi', {
      projectName: 'PosPipeline.IngestApi',
      description: 'Validates incoming POS reports and stores the raw message in S3',
      environment: {
        REPORTS_BUCKET_NAME: reportsBucket.bucketName,
      },
    });

    ingestFunction.addToRolePolicy(
      new iam.PolicyStatement({
        actions: ['s3:PutObject'],
        resources: [reportsBucket.arnForObjects(`${RAW_PREFIX}*`)],
      }),
    );

    const parserFunction = new DotNetFunction(this, 'Parser', {
      projectName: 'PosPipeline.Parser',
      description: 'Parses raw POS reports into structured records and queues them for calculation',
      timeout: Duration.seconds(60),
      deadLetterQueue: parserDeadLetterQueue,
      environment: {
        REPORTS_BUCKET_NAME: reportsBucket.bucketName,
        PARSED_REPORTS_TABLE_NAME: parsedReportsTable.tableName,
        CALCULATION_QUEUE_URL: calculationQueue.queueUrl,
      },
    });

    parserFunction.addToRolePolicy(
      new iam.PolicyStatement({
        actions: ['s3:GetObject'],
        resources: [reportsBucket.arnForObjects(`${RAW_PREFIX}*`)],
      }),
    );
    parserFunction.addToRolePolicy(
      new iam.PolicyStatement({
        actions: ['s3:PutObject'],
        resources: [reportsBucket.arnForObjects(`${ATTACHMENT_PREFIX}*`)],
      }),
    );
    parserFunction.addToRolePolicy(
      new iam.PolicyStatement({
        actions: ['dynamodb:PutItem'],
        resources: [parsedReportsTable.tableArn],
      }),
    );
    parserFunction.addToRolePolicy(
      new iam.PolicyStatement({
        actions: ['sqs:SendMessage'],
        resources: [calculationQueue.queueArn],
      }),
    );

    // Only objects under pos/ trigger the parser, so the attachments and results it writes
    // to the same bucket cannot trigger it again.
    reportsBucket.addEventNotification(
      s3.EventType.OBJECT_CREATED,
      new s3n.LambdaDestination(parserFunction),
      { prefix: RAW_PREFIX },
    );

    const calculatorFunction = new DotNetFunction(this, 'Calculator', {
      projectName: 'PosPipeline.Calculator',
      description: 'Estimates remaining flight time and fuel at arrival for parsed POS reports',
      timeout: CALCULATOR_TIMEOUT,
      environment: {
        REPORTS_BUCKET_NAME: reportsBucket.bucketName,
        PARSED_REPORTS_TABLE_NAME: parsedReportsTable.tableName,
        RESULTS_TABLE_NAME: resultsTable.tableName,
      },
    });

    calculatorFunction.addToRolePolicy(
      new iam.PolicyStatement({
        actions: ['s3:GetObject'],
        resources: [reportsBucket.arnForObjects(`${ATTACHMENT_PREFIX}*`)],
      }),
    );
    calculatorFunction.addToRolePolicy(
      new iam.PolicyStatement({
        actions: ['s3:PutObject'],
        resources: [reportsBucket.arnForObjects(`${RESULT_PREFIX}*`)],
      }),
    );
    calculatorFunction.addToRolePolicy(
      new iam.PolicyStatement({
        actions: ['dynamodb:GetItem'],
        resources: [parsedReportsTable.tableArn],
      }),
    );
    calculatorFunction.addToRolePolicy(
      new iam.PolicyStatement({
        actions: ['dynamodb:PutItem'],
        resources: [resultsTable.tableArn],
      }),
    );

    calculatorFunction.addEventSource(
      new SqsEventSource(calculationQueue, {
        batchSize: 10,
        maxBatchingWindow: Duration.seconds(5),
        // Only the messages that actually failed are returned to the queue.
        reportBatchItemFailures: true,
      }),
    );

    const statusFunction = new DotNetFunction(this, 'StatusApi', {
      projectName: 'PosPipeline.StatusApi',
      description: 'Returns the latest known status of a flight',
      environment: {
        REPORTS_BUCKET_NAME: reportsBucket.bucketName,
        PARSED_REPORTS_TABLE_NAME: parsedReportsTable.tableName,
        RESULTS_TABLE_NAME: resultsTable.tableName,
      },
    });

    statusFunction.addToRolePolicy(
      new iam.PolicyStatement({
        actions: ['s3:GetObject'],
        resources: [reportsBucket.arnForObjects(`${RESULT_PREFIX}*`)],
      }),
    );
    statusFunction.addToRolePolicy(
      new iam.PolicyStatement({
        actions: ['dynamodb:Query'],
        resources: [parsedReportsTable.tableArn, resultsTable.tableArn],
      }),
    );

    // ---------------------------------------------------------------- api

    const api = new apigateway.RestApi(this, 'PosReportApi', {
      restApiName: `${this.stackName}-api`,
      description: 'Receives POS reports and reports flight status',
      // Avoids the account-wide API Gateway CloudWatch role; the Lambdas do the logging.
      cloudWatchRole: false,
      deployOptions: {
        stageName: 'prod',
        throttlingRateLimit: 20,
        throttlingBurstLimit: 40,
      },
    });

    api.root
      .addResource('pos-reports')
      .addMethod('POST', new apigateway.LambdaIntegration(ingestFunction));

    api.root
      .addResource('status')
      .addResource('{flightId}')
      .addMethod('GET', new apigateway.LambdaIntegration(statusFunction));

    // ---------------------------------------------------------------- outputs

    new CfnOutput(this, 'ApiUrl', {
      value: api.url,
      description: 'Base URL of the API',
    });

    new CfnOutput(this, 'PostPosReportCommand', {
      value: `curl -X POST ${api.url}pos-reports -H "Content-Type: text/plain" --data "POS/UL204.FR RGN/TO BKK/041205/N1642.3E09612.5/450/12500/2800"`,
      description: 'Ready-to-run request that submits the example POS report',
    });

    new CfnOutput(this, 'ReportsBucketName', {
      value: reportsBucket.bucketName,
      description: 'Bucket holding pos/, attachment/ and results/ objects',
    });

    new CfnOutput(this, 'ParsedReportsTableName', {
      value: parsedReportsTable.tableName,
      description: 'DynamoDB table of parsed POS reports',
    });

    new CfnOutput(this, 'ResultsTableName', {
      value: resultsTable.tableName,
      description: 'DynamoDB table of calculation results',
    });

    new CfnOutput(this, 'CalculationQueueUrl', {
      value: calculationQueue.queueUrl,
      description: 'Queue between the parser and the calculator',
    });

    new CfnOutput(this, 'CalculationDeadLetterQueueUrl', {
      value: calculationDeadLetterQueue.queueUrl,
      description: 'Calculation messages that failed MAX_RECEIVE_COUNT times',
    });

    new CfnOutput(this, 'ParserDeadLetterQueueUrl', {
      value: parserDeadLetterQueue.queueUrl,
      description: 'S3 events the parser could not process',
    });
  }
}
