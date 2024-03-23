# Convert to PDF with AWS Lambda Function and F# 

[TOC]

## Introduction
This project is inspired by Madhav Palshikar's article [Converting Office Docs to PDF with AWS Lambda](https://madhavpalshikar.medium.com/converting-office-docs-to-pdf-with-aws-lambda-372c5ac918f1). The primary goal of this project is to create a POC solution for converting specified kind of documents, such as Microsoft Word, Excel, PowerPoint and csv, to PDF format using AWS Lambda Functions. In this project, I use .NET 6 and F# to develop the same functionality as described in the original article, but with some key differences.

The source code of Madhav [Palshikar's article](https://gist.github.com/madhavpalshikar/96e72889c534443caefd89000b2e69b5), which is implement by Nood.js. Some libraries are not available in .NET 6.

## Feature
- Convert (Word, Excel, PowerPoint) to PDF format.
- Leverage the power of AWS Lambda Functions for serverless computing
- Use F# and .NET 6 for being a implementation reference in .NET platform.
- Deploy the solution using a custom-built Docker image, which also being a implementation reference in .NET platform.

## Build
### Build Environment
- Platform: x86/x64/arm64: target needs to be x64.
- MacOS/Windows/Linux: I am using MacOS
- .NET 6+ SDK
- AWS CLI
- Amazon.Lambda.Tools 
```bash
# Install Amazon.Lambda.Tools Global Tools if not already installed.
dotnet tool install -g Amazon.Lambda.Tools 

# If already installed check if new version is available.
dotnet tool update -g Amazon.Lambda.Tools
```

- Amazon.Lambda.Templates: used for creating new function, no need for building this project.
```bash
dotnet new --install Amazon.Lambda.Templates 
dotnet new --list --tag AWS   # list Amazon.Lambda.Templates installed template

# Create a new Lambda.Function project
dotnet new lambda.EmptyFunction --name LambdaDemo -lang "F#"
```


### Build Application
1. git clone this project
2. Go to the source project folder `src/convert-to-pdf/` then execute following:
```bash
dotnet build
```

### Build Docker Image
For building lambda application image, an image with building environment is needed. so we have two section, one is application image, the other is building image.

Go to `src/convert-to-pdf/` the execute following command to build application image. building image would be built and be used during building process.
```bash
docker build -t convert-to-pdf .
```

#### Application Image
- Use `public.ecr.aws/lambda/dotnet:6` as application base image, provided by AWS,
    * https://gallery.ecr.aws/lambda/dotnet
    * https://hub.docker.com/layers/amazon/aws-lambda-dotnet/6/images/sha256-33027f8e27f1c09b79550d0e58fa5e054017db175b7838193c7cf3d37c46e9ad?context=explore

- [Base images for Lambda](https://docs.aws.amazon.com/lambda/latest/dg/runtimes-images.html)
- Official documentation for .NET base image, it misses the part of building application.: [Deploy .NET Lambda functions with container images](https://docs.aws.amazon.com/lambda/latest/dg/csharp-image.html)
- Refer to https://stackoverflow.com/questions/65884502/aws-lambda-tar-file-extraction-doesnt-seem-to-work to put LibreOffice installation in built docker image
- Install LibreOffice before building the application, so the installation of LibreOffice could be cached. It can save a lot of building time.

#### Building Image
- Use dotnet6 sdk image to build application for building Lambda application https://hub.docker.com/_/microsoft-dotnet-sdk/?tab=description
- dotnet official sdk image reference repo https://github.com/dotnet/dotnet-docker/blob/main/samples/build-in-sdk-container.md
- The command of build application for the target image which have been placed in `Dockerfile`
```bash
dotnet publish -c Release -o /app/publish --framework "net6.0" /p:GenerateRuntimeConfigurationFiles=true --runtime linux-x64 --self-contained False
```

- In above command, specify `"net6.0"` since current(2024-04) AWS provided .NET base image only support .NET 6.0. 
- In above command, specify `linux-x64` option in dotnet publish command https://learn.microsoft.com/zh-tw/dotnet/core/deploying/ready-to-run
- Some reference for build .NET image for Lambda function:
    * [C# and AWS Lambdas, Part 6 – .NET 5 inside a Container inside a Lambda](https://nodogmablog.bryanhogan.net/2021/03/c-and-aws-lambdas-part-6-net-5-inside-a-container-inside-a-lambda/?utm_source=pocket_saves)
    * [C# and AWS Lambdas, Part 8 - .NET 6, inside a Container, inside a Lambda](https://nodogmablog.bryanhogan.net/2021/03/c-and-aws-lambdas-part-8-net-6-inside-a-container-inside-a-lambda/?utm_source=pocket_saves)


## Test
### Unit test
Execute unit tests
1. Go to the test project folder `test/convert-to-pdf.Tests/` then execute following command: 
```bash
dotnet test
```

### Integration test
Not provided.


## Deploy

### AWS Account Setup
Since we need to deploy to AWS. we need an IAM account which have appropriate permission to execute relate operation on AWS.

#### Configure your IAM account
- The necessary policies for creating AWS Lambda Function:
  * "IAMFullAccess" : for modifying policy.
  * "AWSLambda_FullAccess" : for creating Lambda Function.
  * "AmazonS3FullAccess" : for accessing S3.

### AWS CLI Setup
Before the deployment, please make sure to configure your AWS credentials and settings per the AWS CLI documentation.

- The profile would be different by your AWS account. 
- Your `.aws/config` should include the profile which AWS CLI would use, following content is set up by `aws configure sso`
```
[profile ${your-profile-name}]
sso_start_url = ${your-start-url}
sso_region = ${your-sso-region}
sso_account_id = ${your-sso-sccount-id}
sso_role_name = ${your-role-name}
region = ${your-region}
output = json
```

- You can set default profile for building and running aws command for this application by adding a environment variable in your `.zshrc`.
```bash
export AWS_PROFILE=${your-profile-name}
```

- After you completed the setup above, you can use `aws sso login` to log in your AWS sso account in cli. Then you can execute aws command.

### Deploy to AWS Lambda

#### 1. Deploy to ECR then deploy to AWS Lambda by UI
- Use `dotnet lambda push-image` to publish image to ECR the image name would be the project name by default.
- Then you need to manually click "Deploy New Image" button to deploy the newest image to Lambda Function.
- Need to modify timout from 3sec to 30sec and memory size to 512MB.

#### 2. Deploy to ECR then deploy to AWS Lambda by command
```bash
# One line to build image, push to ECR then deploy function, it can save a lot of time.
dotnet lambda deploy-function --function-name convert-to-pdf --package-type image --function-architecture x86_64 --function-memory-size 512 --function-timeout 30

# Invoke function from cli to verify
dotnet lambda invoke-function convert-to-pdf --payload "Financial\u0020Sample.xlsx"  
dotnet lambda invoke-function convert-to-pdf --payload "sample-docx-file-for-testing.docx"
```

- Need to specify memory size and timeout, using the default would not be appropriate.
- Need to specify the execution role, I use the one which AWS generated for this function then added following policies:
    - After you have privilege to create and set up a role, configure a role which allow Lambda Function to access S3
        * s3:GetObject
        * s3:PutObject

- You can deploy a function with default role then modify that role to grant the access of S3


#### 3. Deploy function to AWS Lambda with zip package 
Used for verify some basic functionality in AWS Lambda, not final implementation.
```bash
cd src/convert-to-pdf
dotnet lambda deploy-function convert-to-pdf-test
```

I tried this way since I think I can use Lambda *Layer* to provide LibreOffice in the function. But I failed. The reason is that I can't get the LibreOffice be untared then be executed by my code. It turned up the only way the code can run LibreOffice successfully is by installing LibreOffice an its dependent package to the base image. So I need to deploy the function with built image.


### Prepare S3 Bucket
- I got AmazonS3FullAccess, you should have getObject and putObject at least.
- Configure Lambda Function's role to access S3 bucket: https://repost.aws/zh-Hant/knowledge-center/lambda-execution-role-s3-bucket
- create a bucket `iqc-convert-to-pdf`
- create a folder `iqc-convert-to-pdf/in`, for incoming file.
- create a folder `iqc-convert-to-pdf/out`, for converted file.
- since this Lambda function is for POC, the bucket is hard-coded in code. 


## Run

#### Run Created Lambda Function convert-to-pdf
- Put the file needed to be converted in `/in` folder
- Invoke the function with the filename by following command.
```bash
dotnet lambda invoke-function ${function-name} --payload "filename.xlsx"
dotnet lambda invoke-function convert-to-pdf --payload "Financial\u0020Sample.xlsx"
```

#### Manual test in local docker environment
The AWS base images for Lambda include the runtime interface emulator. you can use following command to invoke your Lambda function in local docker environment. it can save you a lot of time. 
```bash
docker build -t convert-to-pdf . # build image
docker run -p 9000:8080 convert-to-pdf:latest #run built image
curl -XPOST "http://localhost:9000/2015-03-31/functions/function/invocations" -d '"Financial Sample.xlsx"' # Call API to invoke the function.
docker ps                             # find out the running container id.
docker exec -it ${container-id} bash  # Get into container to check  
```

Refer to following links to get more information:
- https://docs.aws.amazon.com/lambda/latest/dg/images-test.html
- https://docs.aws.amazon.com/lambda/latest/dg/csharp-image.html

## Implementation Notes
- Function.fs - Code file containing the function handler method
- aws-lambda-tools-defaults.json - default argument settings for use with Visual Studio and command line deployment tools for AWS
- Write custom serializer: https://aws.amazon.com/blogs/compute/introducing-the-net-6-runtime-for-aws-lambda/
- Lambda Function input and output: https://docs.aws.amazon.com/lambda/latest/dg/csharp-handler.html#csharp-handler-types
- [ ] Consider to use AWS EventBridge to forward S3 new Object event to Lambda. When a new file is put to S3 then function is triggered to convert. The function need to process S3Event or some kind of Event.
- [ ] Consider to convert the .NET version to 8.0, the performance metric is bad. ~=200MB memory, 15 seconds, which is larger and slower than Node.js version.
- [ ] Consider to use QuestPDF, Spire.Office, Aspose or other 3rd party libraries, but the original requirement is to read office document...


## Not Used
### Add a libreoffice Layer for conversion
- Make sure you have enough privilege for adding a layer. I got `AWSLambda_FullAccess`.
- Add a layer with libreoffice bundle into created Function for conversion functionality, notice that this layer should be in same region with Function.
- Please refer to https://github.com/shelfio/libreoffice-lambda-layer to pick up an ARN which matches Function's region.
- If there is not available layer in the region, you need to publish the layer by yourself.
```bash
git clone https://github.com/shelfio/libreoffice-lambda-layer
git lfs fetch --all # download meta data
git lfs pull        # pull libreoffice bundle. 
```

- Modify `libreoffice-lambda-layer/publish.sh` for deployment layer from local
```bash
#!/usr/bin/env bash

LO_VERSION=6.4.0.1

LAYER_FILENAME=layer.tar.gz.zip
LAYER_NAME=libreoffice-gzip
TARGET_REGION=ap-northeast-1
TARGET_S3_BUCKET=convert-to-pdf

aws s3 cp \
  ./"$LAYER_FILENAME" \
  s3://"$TARGET_S3_BUCKET"/"$LAYER_FILENAME"

aws lambda add-layer-version-permission \
  --region "$TARGET_REGION" \
  --layer-name "$LAYER_NAME" \
  --statement-id sid1 \
  --action lambda:GetLayerVersion \
  --principal '*' \
  --version-number "$(aws lambda publish-layer-version \
    --region "$TARGET_REGION" \
    --layer-name "$LAYER_NAME" \
    --description "${LAYER_NAME} ${LO_VERSION} binary" \
    --query Version \
    --output text \
    --content S3Bucket=${TARGET_S3_BUCKET},S3Key="$LAYER_FILENAME"
    )"

```

- Publish layer by run `libreoffice-lambda-layer/publish.sh`
- You should see following message if IAM role is set up properly.
```json
{
    "Statement": "{\"Sid\":\"sid1\",\"Effect\":\"Allow\",\"Principal\":\"*\",\"Action\":\"lambda:GetLayerVersion\",\"Resource\":\"arn:aws:lambda:ap-northeast-1:${your-id}:layer:libreoffice-gzip:1\"}",
    "RevisionId": "${your-RevisionId}"
}
```


