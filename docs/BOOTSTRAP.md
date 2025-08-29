# Volume Manager App Specification
**Build a minimal C# Windows application that manages per-application volume settings with these core features:**

**What it should do:**
- Run as a system tray application with a beautiful, minimalist main window
- Monitor when users change volume levels for any application in Windows Volume Mixer
- Detect when applications start and automatically restore their previously saved volume levels
- Persist volume settings between Windows sessions
- Show a clean tray icon with right-click context menu (open window, exit)

**UI expectations:**
- **Tray icon:** Simple volume icon with context menu
- **Main window:** Beautiful, minimalist design with two main sections:
    - **Home tab:** Clean dashboard showing detected applications with their saved volume levels, simple controls to manually adjust or clear saved volumes
    - **Logs tab:** Real-time activity feed showing what the program is doing (volume changes detected, apps launched, volumes restored, etc.)
    - Modern aesthetic (think Windows 11/Fluent Design - clean typography, subtle shadows, lots of whitespace)
    - Window hides to tray when closed, doesn't exit the application

**Key requirements:**
- Use Windows Core Audio APIs (suggest NAudio library for easier implementation)
- Store settings in a simple JSON file or similar lightweight format
- Minimal, clean code - prioritize working functionality over fancy features
- Handle the most common applications (Chrome, Spotify, games, etc.)
- Graceful error handling for audio system access
- Real-time logging with timestamps for user visibility into what's happening

**Technical approach:**
- Start with detecting volume changes first, then add application launch detection
- Focus on getting the core loop working: detect → store → restore
- Use async/await for responsiveness
- Target .NET 9 with WinUI for the beautiful interface

**My motto: Less is more.** Build the simplest version that solves the problem completely. No over-engineering, but make it genuinely beautiful and user-friendly. Clean, functional UI that's a pleasure to look at. Just a reliable tool that works silently and shows you exactly what it's doing when you want to see it.
