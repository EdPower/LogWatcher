# LogWatcher

LogWatcher is the code for the csharplog.com blog where I discuss a .Net Core app suite that captures and views log records so that support and dev staff can monitor log and trace activity in real time from remote customer applications.

## Overview
Logwatcher is comprised of LogHost, which is a .Net 7 Core host of several minimal web APIs. These receive log records from remote applications and echo these back via HTTP and SignalR endpoints. LogHost manages the records in a local Sqlite database using Entity Framework.

LogViewer is a .Net 7 WPF desktop app that connects to LogHost and displays log records as they're received by LogHost.

LogSender is a simple .Net 7 WPF desktop app that generates dummy log records and forwards them to LogHost.

## Run Locally

Download the apps to your machine and run them from Visual Studio (community edition is fine). Click the Start button on LogSender to begin sending records to LogHost, and click the Start button on LogViewer to begin receiving updates from LogHost.
