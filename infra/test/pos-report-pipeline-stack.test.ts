import { App } from 'aws-cdk-lib';
import { Match, Template } from 'aws-cdk-lib/assertions';
import { PosReportPipelineStack } from '../lib/pos-report-pipeline-stack';

/**
 * These assertions cover the parts of the stack that are easy to break silently: the S3
 * trigger's prefix filter, the dead-letter queues, the table keys, and the promise that no
 * function is granted more than it needs.
 *
 * Synthesis compiles the C# projects, so the first run is slow and the rest reuse the asset.
 */
describe('PosReportPipelineStack', () => {
  const template = Template.fromStack(new PosReportPipelineStack(new App(), 'TestStack'));

  test('the API exposes exactly the two documented routes', () => {
    template.resourceCountIs('AWS::ApiGateway::RestApi', 1);

    template.hasResourceProperties('AWS::ApiGateway::Resource', { PathPart: 'pos-reports' });
    template.hasResourceProperties('AWS::ApiGateway::Resource', { PathPart: 'status' });
    template.hasResourceProperties('AWS::ApiGateway::Resource', { PathPart: '{flightId}' });

    template.resourcePropertiesCountIs('AWS::ApiGateway::Method', { HttpMethod: 'POST' }, 1);
    template.resourcePropertiesCountIs('AWS::ApiGateway::Method', { HttpMethod: 'GET' }, 1);
  });

  test('all four handlers run the same C# runtime', () => {
    const functions = template.findResources('AWS::Lambda::Function', {
      Properties: { Runtime: 'dotnet8' },
    });

    expect(Object.keys(functions)).toHaveLength(4);

    for (const handler of Object.values(functions)) {
      expect(handler.Properties.Architectures).toEqual(['arm64']);
      expect(handler.Properties.Handler).toMatch(/^PosPipeline\.\w+::PosPipeline\.\w+\.Function::HandleAsync$/);
    }
  });

  test('the parser is triggered only by new objects under the pos/ prefix', () => {
    template.hasResourceProperties('Custom::S3BucketNotifications', {
      NotificationConfiguration: {
        LambdaFunctionConfigurations: [
          Match.objectLike({
            Events: ['s3:ObjectCreated:*'],
            Filter: { Key: { FilterRules: [{ Name: 'prefix', Value: 'pos/' }] } },
          }),
        ],
      },
    });
  });

  test('both tables are keyed by flight and timestamp', () => {
    const tables = template.findResources('AWS::DynamoDB::Table');

    expect(Object.keys(tables)).toHaveLength(2);

    for (const table of Object.values(tables)) {
      expect(table.Properties.KeySchema).toEqual([
        { AttributeName: 'flightId', KeyType: 'HASH' },
        { AttributeName: 'timestamp', KeyType: 'RANGE' },
      ]);
      expect(table.Properties.BillingMode).toBe('PAY_PER_REQUEST');
    }
  });

  test('the calculation queue moves repeatedly failing messages to a dead-letter queue', () => {
    template.hasResourceProperties('AWS::SQS::Queue', {
      RedrivePolicy: Match.objectLike({ maxReceiveCount: 3 }),
    });

    // The calculation queue, its dead-letter queue, and the parser's dead-letter queue.
    template.resourceCountIs('AWS::SQS::Queue', 3);
  });

  test('the calculator reports individual message failures rather than whole batches', () => {
    template.hasResourceProperties('AWS::Lambda::EventSourceMapping', {
      FunctionResponseTypes: ['ReportBatchItemFailures'],
    });
  });

  test('the parser sends events it cannot process to its own dead-letter queue', () => {
    template.hasResourceProperties('AWS::Lambda::Function', {
      Handler: 'PosPipeline.Parser::PosPipeline.Parser.Function::HandleAsync',
      DeadLetterConfig: { TargetArn: Match.anyValue() },
    });
  });

  test('the bucket is private and encrypted', () => {
    template.hasResourceProperties('AWS::S3::Bucket', {
      BucketEncryption: Match.objectLike({
        ServerSideEncryptionConfiguration: [
          { ServerSideEncryptionByDefault: { SSEAlgorithm: 'AES256' } },
        ],
      }),
      PublicAccessBlockConfiguration: {
        BlockPublicAcls: true,
        BlockPublicPolicy: true,
        IgnorePublicAcls: true,
        RestrictPublicBuckets: true,
      },
    });
  });

  test('no policy in the stack allows an action on every resource', () => {
    const policies = {
      ...template.findResources('AWS::IAM::Policy'),
      ...template.findResources('AWS::IAM::Role'),
    };

    const statements = Object.values(policies).flatMap((policy) => [
      ...(policy.Properties.PolicyDocument?.Statement ?? []),
      ...(policy.Properties.Policies ?? []).flatMap((inline: any) => inline.PolicyDocument.Statement),
    ]);

    expect(statements.length).toBeGreaterThan(0);

    for (const statement of statements) {
      if (statement.Effect === 'Deny') {
        continue;
      }

      const asList = (value: unknown) => (Array.isArray(value) ? value : [value]);

      // No "Resource": "*" and no "Action": "s3:*" anywhere in the stack.
      expect(asList(statement.Resource)).not.toContain('*');
      for (const action of asList(statement.Action)) {
        expect(action).not.toBe('*');
        expect(typeof action === 'string' ? action : '').not.toMatch(/:\*$/);
      }
    }
  });

  test('the handler roles carry only the inline policies this stack grants them', () => {
    const roles = Object.entries(template.findResources('AWS::IAM::Role')).filter(([logicalId]) =>
      /^(IngestApi|Parser|Calculator|StatusApi)Role/.test(logicalId),
    );

    expect(roles).toHaveLength(4);

    for (const [, role] of roles) {
      // No AWSLambdaBasicExecutionRole: each function may write to its own log group only.
      expect(role.Properties.ManagedPolicyArns).toBeUndefined();
      expect(role.Properties.Policies).toHaveLength(1);
      expect(role.Properties.Policies[0].PolicyName).toBe('WriteOwnLogs');
    }
  });

  test('the ingest function may only write raw reports, and cannot read anything back', () => {
    const policies = Object.values(template.findResources('AWS::IAM::Policy'));

    const ingestPolicy = policies.find((policy) =>
      JSON.stringify(policy).includes('IngestApiRole'),
    );

    expect(ingestPolicy).toBeDefined();

    const actions = ingestPolicy!.Properties.PolicyDocument.Statement.flatMap((statement: any) =>
      Array.isArray(statement.Action) ? statement.Action : [statement.Action],
    );

    expect(actions).toEqual(['s3:PutObject']);
  });
});
