using Pulumi;
using Pulumi.Aws.Iam;
using Pulumi.Aws.Lambda;
using Pulumi.Aws.S3;
using Pulumi.Aws.S3.Inputs;
using System.Collections.Generic;

return await Deployment.RunAsync(() =>
{
    // Create an AWS resource (S3 Bucket)
    var bucket = new Bucket(
        "murphs-awsnorth2026-bucket"
    );

    // Role the Lambda runs as
    var role = new Role(
        "file-mover-role",
        new RoleArgs
        {
            AssumeRolePolicy = @"{
                ""Version"": ""2012-10-17"",
                ""Statement"": [{
                    ""Effect"": ""Allow"",
                    ""Principal"": { ""Service"": ""lambda.amazonaws.com"" },
                    ""Action"": ""sts:AssumeRole""
                }]
            }",
        }
    );

    // Allow the Lambda to write logs
    new RolePolicyAttachment(
        "file-mover-logs", 
        new RolePolicyAttachmentArgs
        {
            Role = role.Name,
            PolicyArn = ManagedPolicy.AWSLambdaBasicExecutionRole.ToString(),
        }
    );

    // Allow the Lambda to read, write and delete objects in the bucket
    new RolePolicy(
        "file-mover-s3",
        new RolePolicyArgs
        {
            Role = role.Id,
            Policy = bucket.Arn.Apply(arn => $@"{{
                ""Version"": ""2012-10-17"",
                ""Statement"": [{{
                    ""Effect"": ""Allow"",
                    ""Action"": [""s3:GetObject"", ""s3:PutObject"", ""s3:DeleteObject""],
                    ""Resource"": ""{arn}/*""
                }}]
            }}"),
        }
    );

    // The Lambda function, built from ../lambda-function (run `npm run build` there first)
    var fn = new Function(
        "file-mover",
        new FunctionArgs
        {
            Runtime = "nodejs22.x",
            Handler = "index.handler",
            Role = role.Arn,
            Code = new FileArchive("../lambda-function/dist"),
        }
    );

    // Allow S3 to invoke the Lambda
    var allowS3 = new Permission(
        "allow-s3",
        new PermissionArgs
        {
            Action = "lambda:InvokeFunction",
            Function = fn.Name,
            Principal = "s3.amazonaws.com",
            SourceArn = bucket.Arn,
        }   
    );

    // Trigger the Lambda when an object is created in the bucket
    new BucketNotification(
        "on-upload",
        new BucketNotificationArgs
        {
            Bucket = bucket.Id,
            LambdaFunctions =
            {
                new BucketNotificationLambdaFunctionArgs
                {
                    LambdaFunctionArn = fn.Arn,
                    Events = { "s3:ObjectCreated:*" },
                },
            },
        }, 
        new CustomResourceOptions { DependsOn = { allowS3 } }
    );

    // Export the name of the bucket
    return new Dictionary<string, object?>
    {
        ["bucketName"] = bucket.Id,
        ["functionName"] = fn.Name,
    };
});
