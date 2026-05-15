#!/bin/sh
# This script runs TANCOUNTER01 in development mode by default.

rm -rf bin obj
dotnet build
dotnet run --no-build
