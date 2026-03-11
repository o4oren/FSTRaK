# FSTRaK — Project Documentation Index

> Generated: 2026-03-11 | Scan Level: Deep | Mode: Initial Scan

## Project Overview

- **Type:** Desktop Application (WPF) — Monolith
- **Primary Language:** C# (.NET Framework 4.7.2)
- **Architecture:** MVVM + State Machine
- **Purpose:** Automatic flight tracker and logbook for Microsoft Flight Simulator

## Quick Reference

- **Framework:** WPF on .NET Framework 4.7.2
- **Database:** SQLite via Entity Framework 6
- **Entry Point:** `App.xaml` → `Views/MainWindow.xaml`
- **Build:** Visual Studio, `x64` platform target required
- **Data Location:** `%LOCALAPPDATA%\FSTRaK` (release) / `%LOCALAPPDATA%\FSTRaK_DEBUG` (debug)

## Generated Documentation

- [Project Overview](./project-overview.md) — What FSTRaK is, key features, technical summary
- [Architecture](./architecture.md) — System design, components, data model, data flow
- [Technology Stack](./technology-stack.md) — Dependencies, versions, architecture pattern
- [Source Tree Analysis](./source-tree-analysis.md) — Annotated directory structure, critical folders
- [State Management](./state-management.md) — State patterns, data flow, singleton services
- [UI Component Inventory](./ui-component-inventory.md) — Views, ViewModels, resources, map components
- [Development Guide](./development-guide.md) — Prerequisites, build, run, common tasks

## Existing Documentation

- [README.md](../README.md) — Project overview, features, roadmap, screenshots
- [CLAUDE.md](../CLAUDE.md) — AI coding assistant context with architecture details

## Getting Started

1. Open `FSTRaK.sln` in Visual Studio
2. Set platform to **x64** (Configuration Manager)
3. Build (`Debug|x64` or `Release|x64`)
4. Run — FSTRaK connects to MSFS automatically when the sim is running
