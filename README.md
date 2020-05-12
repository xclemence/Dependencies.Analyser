# Dependencies Analyser
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](./LICENSE)
[![Build Status][build-status-badge]][build-status-url]

This repository contains analyzers for assemblies dependencies. All analyzers in this repository support **.Net Core 3.1**.

## Global features:
- Scan managed Assemblies (C#)
- Scan native Assemblies (C++)
- Support CLI/C++ Assemblies
- Support Dll import for mix assemblies (native and managed)

## Analyzers

### Native analyzer

The native analyzer support C++ and CLI/C++ assembly files. It uses [PeNet][penet-project] to read Pe file and find native dependencies

All other analyzers use this analyzer for the native part.

### Mono analyzer

This analyzer uses [Mono Cecil][mono-cecil-project] to read .Net assemblies and find all dependencies.

### Microsoft analyzer

This analyzer used the new net core feature to load assemblies in a specific context and MetadataLoadContext to load only assembly metadata.

With these tools, we can load assembly in an isolated context and avoid host application alteration.

[build-status-badge]:   https://xce-account.visualstudio.com/Dependencies.Analyser/_apis/build/status/xclemence.Dependencies.Analyser?branchName=master
[build-status-url]:     https://xce-account.visualstudio.com/Dependencies.Analyser/_build/latest?definitionId=3&branchName=master
[penet-project]:        https://github.com/secana/PeNet
[mono-cecil-project]:   https://github.com/jbevain/cecil