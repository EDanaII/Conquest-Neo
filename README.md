# Conquest (C → C# Modernization)

> A faithful **reimplementation** of the classic text strategy game — modern C#, clean layering, and behavior-first tests.  
> **No bit-exact C parity** or “golden transcript” testing required.

<!-- Optional badges — replace OR remove -->
<!--
[![CI](https://github.com/<you>/<repo>/actions/workflows/ci.yml/badge.svg)](https://github.com/<you>/<repo>/actions/workflows/ci.yml)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)
-->

---

## Quick start

**Prereq:** .NET 8 SDK

```bash
dotnet build
dotnet test

# Example run (CLI):
dotnet run --project Conquest.CLI -- --seed 1 --w 64 --h 64 --count 20 --space 3
