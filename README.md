# Dependencies Analyser
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](./LICENSE)
[![Build Status][build-status-badge]][build-status-url]

This repository contains analysers for assemblies dependencies. All analysers in this repository support **.Net Core 3.1**.

## Global features:
- Managed Assemblies scan (C#)
- Native Assemblies scan (C++)
- CLI/C++ Assemblies scan
- Mix Assemblies scan
- Read Windows Api set map (for native assemblies)

## analysers

### Native analyser

The native analyser support C++ and CLI/C++ assembly files. It uses [PeNet][penet-project] to read Pe file and find native dependencies.

Windows 7 introduce a *proxy* mechanism for assemblies.  this mechanism is called Api Set. the goal is to provide architectural separation between an API contract (reference in assembly) and the associated implementation (dll). So when a Windows reference is found, we need to check in Api Set to find the corresponded dll.

The native analyser provides an implementation for Api Set V6 (Windows 10).

All other analysers use this analyser for the native part.

### Mono analyser

This analyser uses [Mono Cecil][mono-cecil-project] to read .Net assemblies and find all dependencies.

### Microsoft analyser

This analyser used the new net core feature to load assemblies in a specific context and MetadataLoadContext to load only assembly metadata.

With these tools, we can load assembly in an isolated context and avoid host application alteration.

[build-status-badge]:   https://xce-account.visualstudio.com/Dependencies.Analyser/_apis/build/status/xclemence.Dependencies.Analyser?branchName=master
[build-status-url]:     https://xce-account.visualstudio.com/Dependencies.Analyser/_build/latest?definitionId=3&branchName=master
[penet-project]:        https://github.com/secana/PeNet
[mono-cecil-project]:   https://github.com/jbevain/cecil
