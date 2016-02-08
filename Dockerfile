FROM microsoft/aspnet:1.0.0-rc1-update1-coreclr

# Add this toolchain
ENV SRCLIBPATH /srclib

ADD . /srclib/srclib-csharp/
RUN cd /srclib/srclib-csharp && dnu restore
