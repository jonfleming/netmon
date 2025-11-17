# NetMon - Simple Internet Monitor (Windows)

This is a small Windows Forms app that monitors internet connectivity and sounds an audible alarm when the internet is not reachable.

Features:
- Simple UI with Start/Stop button
- Connected/Disconnected status label (color-coded)
- Interval selector (seconds)
- "Check Now" button for instant checking
- Audible alarm plays while the internet is down

Requirements:
- .NET SDK 6.0+ (Windows / desktop workloads) — this project targets .NET 8.0 but should work with .NET 6/7/8 on Windows.

Build and run:
- Open a PowerShell or command prompt on Windows
- Navigate to the project folder: `cd C:\Projects\netmon`
- Build: `dotnet build` 
- Run: `dotnet run` 

How it works:
- When you click Start, the app periodically sends a GET request to `https://clients3.google.com/generate_204` to check if the internet is reachable.
- If the check fails, the UI switches to "Disconnected" and an audible alarm is played repeatedly until the connection is restored or you stop monitoring.

Notes:
- If the alarm doesn't play on your machine, check system audio settings or try running the app with a different sound device.
- The check interval defaults to 5 seconds and can be changed with the Interval control.

This is a minimal demonstration and intended to be a starting point for a more polished tool.
