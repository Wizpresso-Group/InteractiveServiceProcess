# InteractiveProcess

Allows you to run service process that requires interaction by abusing Windows security token and current user session.

# Why?

Sometimes we want to run a service (or child processes of a particular service) with visual elements for good use.
For example, we want to find ways to launch automation tools, verify them and do diagnosis when they went into exceptional state.
Our internal use found it by useful combining [Hashicorp Nomad](https://www.nomadproject.io/) and `raw_exec` mode to migrate our legacy workload
away from an internal scheduler system.

Indeed, we can create [Window station and Desktop via Windows API](https://learn.microsoft.com/en-us/windows/win32/winstation/window-station-and-desktop-creation) and spawn the process that way, but keep in mind of this restriction:

> The interactive window station, Winsta0, is the only window station that can display a user interface or receive user input. 
> It is assigned to the logon session of the interactive user, and contains the keyboard, mouse, and display device. 
> All other window stations are noninteractive, which means they cannot display a user interface or receive user input.

This basically means you can't directly see the newly created desktop, and also effectively means you only get a framebuffer out of all programatically created desktop objects.
We can't afford to add more tools and compute resources for capturing, transcoding and uploading the framebuffer content just to cope with the change.

This utility works by stealing the user token of "last active window session" which is still available on Windows 11. Then we spawn a user process under that token.
If the spawned process accidentally died, it will automatically retry until service stopped.

This program is provided "as-is". User discretion is advised.

# Prerequisite

The service must be running as "Local System"

# TODO

1. Fix priority access, some sessions can still be attached, an educated guess suggested the WTSDisconnected state can still be used to attach the service to a certain account
2. Add "account affinity", so that certain accounts are prioritized to start the service there first 
3. Find a way to auto-login a session, logout and attach the service to that session so that we can automate the CI bot account (currently we are using automated Windows login)
4. Add a "max retry count" and stop the service itself once the limit is reached (just like vanilla Windows service)

# Notice:

Unfortunately Microsoft decided that Session 0 (the very first Windows session at boot time) has to be isolated from the rest of the system that is interactively accessible, starting from Windows Vista.
This restriction only gets more and more restrictive over newer iteractions of Windows. Expect this to also stop working in the long term.