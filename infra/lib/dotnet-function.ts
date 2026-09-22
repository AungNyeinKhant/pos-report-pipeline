import { spawnSync } from 'child_process';
import { existsSync } from 'fs';
import * as path from 'path';
import { DockerImage, Duration, RemovalPolicy, Stack } from 'aws-cdk-lib';
import * as iam from 'aws-cdk-lib/aws-iam';
import * as lambda from 'aws-cdk-lib/aws-lambda';
import * as logs from 'aws-cdk-lib/aws-logs';
import * as sqs from 'aws-cdk-lib/aws-sqs';
import { Construct } from 'constructs';

/** Root of the .NET solution, relative to this file. */
const SOLUTION_DIRECTORY = path.join(__dirname, '..', '..', 'src');

/**
 * Lambda runtime for every function in this app. Changing the .NET version means changing
 * this and `TargetFramework` in src/Directory.Build.props together.
 */
const RUNTIME = lambda.Runtime.DOTNET_8;

/** Container image used when the build has to run in Docker rather than on the host. */
const BUILD_IMAGE = 'public.ecr.aws/sam/build-dotnet8:latest';

export interface DotNetFunctionProps {
  /** Project directory name under src/, e.g. "PosPipeline.IngestApi". */
  readonly projectName: string;

  /** Shown in the console and in the CloudFormation template. */
  readonly description: string;

  readonly environment?: Record<string, string>;

  readonly timeout?: Duration;

  readonly memorySize?: number;

  /** Queue that receives events this function could not process after its retries. */
  readonly deadLetterQueue?: sqs.IQueue;
}

/**
 * A C# Lambda function.
 *
 * It bundles the project on `cdk synth`, so `cdk deploy` is the only command needed: the
 * build runs on the host when the .NET SDK is installed, and falls back to the AWS SAM
 * build container when it is not.
 *
 * Each function gets its own execution role and log group. The role starts with nothing but
 * permission to write to that log group; the stack then grants exactly the resource actions
 * the function needs, which is why no AWS managed policy is attached anywhere in this app.
 */
export class DotNetFunction extends lambda.Function {
  constructor(scope: Construct, id: string, props: DotNetFunctionProps) {
    const functionName = `${Stack.of(scope).stackName}-${id}`;

    const logGroup = new logs.LogGroup(scope, `${id}LogGroup`, {
      logGroupName: `/aws/lambda/${functionName}`,
      retention: logs.RetentionDays.ONE_WEEK,
      removalPolicy: RemovalPolicy.DESTROY,
    });

    const role = new iam.Role(scope, `${id}Role`, {
      assumedBy: new iam.ServicePrincipal('lambda.amazonaws.com'),
      description: `Execution role for ${functionName}`,
      inlinePolicies: {
        // Deliberately not the AWSLambdaBasicExecutionRole managed policy, which allows
        // writing to every log group in the account.
        WriteOwnLogs: new iam.PolicyDocument({
          statements: [
            new iam.PolicyStatement({
              actions: ['logs:CreateLogStream', 'logs:PutLogEvents'],
              resources: [logGroup.logGroupArn],
            }),
          ],
        }),
      },
    });

    super(scope, id, {
      functionName,
      description: props.description,
      runtime: RUNTIME,
      // Graviton: same price per millisecond as x86 on the free tier and cheaper after it.
      // The published assemblies are portable IL, so no architecture-specific build is needed.
      architecture: lambda.Architecture.ARM_64,
      handler: `${props.projectName}::${props.projectName}.Function::HandleAsync`,
      code: buildDotNetProject(props.projectName),
      role,
      logGroup,
      // Structured logs, so CloudWatch Logs Insights can filter on level rather than text.
      loggingFormat: lambda.LoggingFormat.JSON,
      memorySize: props.memorySize ?? 512,
      timeout: props.timeout ?? Duration.seconds(30),
      environment: props.environment,
      deadLetterQueue: props.deadLetterQueue,
    });
  }
}

/**
 * Publishes one project into a Lambda deployment package.
 *
 * The asset is the whole solution directory because the Lambda projects reference the shared
 * PosPipeline.Core and PosPipeline.Aws projects; a per-project asset would not contain them.
 */
function buildDotNetProject(projectName: string): lambda.Code {
  return lambda.Code.fromAsset(SOLUTION_DIRECTORY, {
    exclude: ['**/bin/**', '**/obj/**', '**/TestResults/**'],
    bundling: {
      image: DockerImage.fromRegistry(BUILD_IMAGE),
      command: [
        '/bin/sh',
        '-c',
        `dotnet publish ${projectName} --configuration Release --output /asset-output --nologo` +
          ` && test -f /asset-output/${projectName}.runtimeconfig.json`,
      ],
      environment: {
        // The build container runs as a non-root user without a writable home directory.
        DOTNET_CLI_HOME: '/tmp',
        DOTNET_CLI_TELEMETRY_OPTOUT: '1',
        XDG_DATA_HOME: '/tmp',
      },
      local: {
        tryBundle(outputDirectory: string): boolean {
          const build = spawnSync(
            'dotnet',
            [
              'publish',
              projectName,
              '--configuration',
              'Release',
              '--output',
              outputDirectory,
              '--nologo',
            ],
            {
              cwd: SOLUTION_DIRECTORY,
              stdio: ['ignore', 'inherit', 'inherit'],
            },
          );

          if (build.error || build.status !== 0) {
            // Falling back to Docker; CDK prints this path when it happens.
            return false;
          }

          // The managed .NET runtime refuses to start without this file, and a class library
          // only produces it when GenerateRuntimeConfigurationFiles is set. Catching it here
          // turns a failure that would otherwise appear as a 500 from a deployed function into
          // a build error.
          const runtimeConfig = path.join(outputDirectory, `${projectName}.runtimeconfig.json`);
          if (!existsSync(runtimeConfig)) {
            throw new Error(
              `${projectName} published without ${projectName}.runtimeconfig.json. ` +
                'Set <GenerateRuntimeConfigurationFiles>true</GenerateRuntimeConfigurationFiles> in its .csproj.',
            );
          }

          return true;
        },
      },
    },
  });
}
