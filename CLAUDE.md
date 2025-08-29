# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

VolumeKeeper is a Windows desktop application that manages per-application volume settings. It monitors volume changes in the Windows Volume Mixer, saves them, and automatically restores volume levels when applications launch.

## Development Philosophy & Approach

### Conservative Development Principles
- **Surgical Precision**: Only make changes explicitly requested by the user
- **Minimal Changes**: Make the smallest number of human-understandable changes possible
- **No "Boyscout" Changes**: Avoid fixing unrelated issues unless explicitly requested
- **Honesty Over Pleasing**: Be honest about limitations rather than attempting to please
- **Document Don't Fix**: Note related issues but don't fix them without user request
- **Respect Existing Patterns**: Follow established code patterns even if not optimal

### Pair Programming Approach

Follow these collaborative principles:

#### Critical Thinking Phase
- Reflect independently before proposing solutions
- Consider implications within Windows/WPF constraints
- Identify potential issues with audio APIs and permissions
- Document reasoning and assumptions

#### Consensus Building
- Explicitly state your position and reasoning
- Address concerns before proceeding
- Base decisions on technical merit
- Document compromises and rationale

#### Change Management
- Focus solely on the specific task requested
- Be explicit about what will and won't be changed
- Separate suggestions from current work
- Respect existing WPF/MVVM patterns

## Code Standards

### C# Guidelines
- Use latest C# features (nullable reference types, file-scoped namespaces)
- Follow .NET naming conventions (PascalCase for public members, camelCase for private)
- Use `async`/`await` for all IO operations and UI updates
- Prefer LINQ over loops where readable
- Use dependency injection for testability
- Implement `INotifyPropertyChanged` for data binding
- Use `ConfigureAwait(false)` for non-UI async calls
- Avoid using `this.` prefix unless necessary for disambiguation
- Prefer guard clauses over nested if statements for early returns

### Clean Code Principles

Follow Uncle Bob's clean code principles for maintainable, readable code:

#### Functions and Methods
- **Single Responsibility**: Each method should do one thing well
- **Small Methods**: Keep methods small (ideally < 20 lines)
- **Descriptive Names**: Use intention-revealing names that explain what the method does
- **Method Arguments**: Prefer fewer arguments (0-3 ideal, avoid >4)
- **No Side Effects**: Methods should not have hidden side effects
- **Extract Helper Methods**: Break down complex logic into well-named helper methods

#### Code Organization
- **Extract Configuration**: Use classes to group related settings
- **Eliminate Duplication**: Don't repeat yourself (DRY principle)
- **Meaningful Comments**: Write code that explains itself; use comments sparingly for "why" not "what"
- **Consistent Formatting**: Follow consistent indentation and spacing
- **Error Handling**: Use proper exception handling, especially for Windows API calls

#### Class Design
- **Small Classes**: Classes should have a single reason to change
- **Cohesion**: Class methods should work together toward a common purpose
- **Encapsulation**: Hide implementation details, expose only what's necessary
- **Composition over Inheritance**: Prefer composition when possible
- **MVVM Pattern**: Separate View, ViewModel, and Model concerns

#### Naming Conventions
- **Classes**: Use nouns (e.g., `VolumeManager`, `ApplicationSettings`)
- **Methods**: Use verbs (e.g., `SaveVolumeSettings`, `DetectApplicationLaunch`)
- **Properties**: Use descriptive names (e.g., `CurrentVolume` not `Vol`)
- **Events**: Use EventHandler suffix (e.g., `VolumeChangedEventHandler`)
- **Constants**: Use PascalCase (e.g., `DefaultVolume`)

#### Comment Guidelines
- **AVOID unnecessary comments**: Code should be self-explanatory through good naming and structure
- **Extract methods instead**: If code needs explanation, extract it to a well-named method
- **ONLY comment for unusual/unexpected behavior**: Add comments when code does something non-obvious
  - Example: Windows API quirks or workarounds
  - Example: Audio session management complexities
- **XML Documentation**: Use for public APIs only
- **Remove obsolete comments**: Keep comments accurate and current

#### Refactoring Guidelines
- **Extract Method**: When logic becomes complex, extract into helper methods
- **Extract Class**: When a class has too many responsibilities
- **Extract Interface**: For testability and loose coupling
- **Replace Magic Numbers**: Use named constants for hardcoded values

## Before Making Changes

### Reflection Checklist
1. **Understand the Request**: What exactly is being asked? Is it clear and specific?
2. **Assess Impact**: What modules/files will be affected? Are there dependencies?
3. **Consider Alternatives**: Are there simpler approaches? What are the trade-offs?
4. **Plan Minimally**: What's the smallest change that addresses the request?
5. **Identify Risks**: What could break? What threading issues exist?

### Change Validation
- Is this change compatible with the target .NET version?
- Are we following best practices and MVVM patterns?
- Is error handling adequate for Windows API calls?
- Does this maintain responsiveness (no blocking UI thread)?

## Key Commands

### Building and Running
```bash
# Build the project
dotnet build

# Run the application
dotnet run --project VolumeKeeper

# Build in Release mode
dotnet build -c Release

# Publish for release
dotnet publish -c Release

# Run tests (when implemented)
dotnet test
```

### Development Requirements
- .NET 9.0 SDK
- Windows OS (net9.0-windows target)
- Visual Studio 2022 or VS Code with C# extensions

## Architecture

The project follows a WinUI 3 application structure with MVVM implementation:

### Current Components
- **App.xaml/App.xaml.cs**: WinUI 3 application entry point with system tray integration
- **MainWindow.xaml/MainWindow.xaml.cs**: Main UI window with NavigationView for Home and Logs tabs
- **HomePage.xaml/HomePage.xaml.cs**: Volume management tab with application list and controls
- **LogsPage.xaml/LogsPage.xaml.cs**: Activity feed tab showing real-time logs

### Planned Architecture (per specification)

#### Core Components
1. **Audio Management Layer**
   - Windows Core Audio API wrapper (using NAudio)
   - Volume monitoring service
   - Application detection service
   - Volume restoration service

2. **Data Layer**
   - Settings persistence (JSON)
   - Application volume profiles
   - Configuration management

3. **UI Layer**
   - System tray integration
   - Main window with tabs (Home, Logs)
   - MVVM ViewModels for data binding
   - Real-time logging display

4. **Services**
   - Background monitoring service
   - Application launch detection
   - Volume change detection and persistence
   - Logging service with timestamps

### Design Principles
- **MVVM Pattern**: Separate UI from business logic
- **Async Operations**: Keep UI responsive
- **Service Pattern**: Encapsulate audio and persistence logic
- **Observer Pattern**: For volume change notifications
- **Repository Pattern**: For settings persistence

## Development Guidelines

- Target .NET 9.0 with WinUI 3 for UI
- Use async/await for all potentially blocking operations
- Implement core functionality in order: detect → store → restore
- Focus on minimal, clean implementation
- Handle audio system access errors gracefully
- Provide real-time logging for user visibility
- Ensure proper disposal of audio resources
- Handle Windows session changes (lock/unlock, sleep/wake)

## Current Status

The project is in initial setup phase with basic WPF scaffolding. Next steps include:
1. Adding NAudio NuGet package for audio management
2. Implementing system tray functionality
3. Creating the volume monitoring service
4. Building the UI according to the minimalist design specification
5. Implementing MVVM architecture with proper ViewModels
