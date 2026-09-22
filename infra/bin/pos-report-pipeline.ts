#!/usr/bin/env node
import { App } from 'aws-cdk-lib';
import { PosReportPipelineStack } from '../lib/pos-report-pipeline-stack';

const app = new App();

new PosReportPipelineStack(app, app.node.tryGetContext('stackName') ?? 'PosReportPipeline', {
  // Falls back to whatever the current CLI credentials and profile point at, so the app is
  // environment-agnostic and needs no account ID committed to the repository.
  env: {
    account: process.env.CDK_DEFAULT_ACCOUNT,
    region: process.env.CDK_DEFAULT_REGION,
  },
  description: 'Serverless POS report ingest, parsing, calculation and status lookup',
});
