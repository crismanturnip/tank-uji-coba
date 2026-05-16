#!/bin/sh
# This script runs Tatsuya in development mode by default.

rm -rf bin obj
dotnet build
dotnet run --no-build
