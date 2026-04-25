FROM ubuntu:latest

RUN apt-get update \
&& apt-get upgrade -y --force-yes \
&& apt-get install -y --force-yes dotnet-sdk-10.0 \
&& apt-get clean

RUN dotnet tool install -g JetBrains.ReSharper.GlobalTools

FROM unityci/editor:ubuntu-6000.4.3f1-webgl-3.2.2

RUN jb inspectcode Unity/Unity.sln --eXtensions=JetBrains.Unity --output=inspectcode.txt
