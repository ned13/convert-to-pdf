# FROM public.ecr.aws/lambda/dotnet:6 AS base
FROM --platform=linux/amd64 public.ecr.aws/lambda/dotnet:6 AS base
#You can also pull these images from DockerHub amazon/aws-lambda-dotnet:7

# - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
# Download and install LibreOffice (and deps)
# Put this part before building application, it can speed up the building time since the build result would not change,
# So it can be cached.
RUN yum update -y \
    && yum clean all \
    && yum install -y wget tar gzip

RUN cd /tmp \
    && wget http://download.documentfoundation.org/libreoffice/stable/7.4.6/rpm/x86_64/LibreOffice_7.4.6_Linux_x86-64_rpm.tar.gz \
    && tar -xvf LibreOffice_7.4.6_Linux_x86-64_rpm.tar.gz \
    && rm LibreOffice_7.4.6_Linux_x86-64_rpm.tar.gz
# remove downloaded rpm within same RUN command for saving space

# For some reason we need to "clean all"
RUN cd /tmp/LibreOffice_7.4.6.2_Linux_x86-64_rpm/RPMS \
    && yum clean all \
    && yum -y localinstall *.rpm \
    && cd /tmp \
    && rm -rf LibreOffice_7.4.6.2_Linux_x86-64_rpm/ 
# remove untared directory within same RUN commend for saving space

# Required deps for soffice
RUN yum -y install \
    fontconfig libXinerama.x86_64 cups-libs dbus-glib cairo libXext libSM libXrender

# Update PATH env, so the libreoffice can be invoke without path
ENV PATH="${PATH}:/opt/libreoffice7.4/program"

# remove this line after the developement is completed.
COPY ["./Financial Sample.xlsx", "/tmp"]  



FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build

WORKDIR /app
COPY . .
# RUN pwd       # For checking env.
# RUN ls -la    # For checking env.

# there is no point doing the restore as its own step because the dotnet publish doesn't work with the --no-restore flag.
# COPY *.csproj .
# RUN dotnet restore

# --runtime linux-x64 is used for optimization in running env, since the AWS Lambda I choosed is x64, so use linux-x64.
RUN dotnet publish -c Release -o /app/publish --framework "net6.0" /p:GenerateRuntimeConfigurationFiles=true --runtime linux-x64 --self-contained False

 
FROM base AS final
WORKDIR ${LAMBDA_TASK_ROOT}
COPY --from=build /app/publish .


# Set the CMD to your handler (could also be done as a parameter override outside of the Dockerfile)
CMD [ "convert-to-pdf::ConvertToPdf.Function::FunctionHandler" ]