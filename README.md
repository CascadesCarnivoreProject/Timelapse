### Overview
This repository contains the source code for and releases of Carnassial and its associated tenplate editor. Refer to Github's wiki tab above to 
find out how to get started and learn more about Carnassial.

### Contributing
Bug reports, feature requests, and feedback are most welcome. Let us know!  We'd also really appreciate sample images and videos to test our 
code on and expand some features. Shoot us an email at carnassialdev@gmail.com if you've some you'd like to share. We're particularly looking 
for samples from

* Bushnell Trophy or Trophy HD cameras from 2013 back to 2006
* Reconyx HyperFire, UltraFire, MicroFire, and RapidFire cameras
* SpyPoint Force-10D, 11D, and other cameras from 2016 or newer

Having these in our archives helps us help you, so don't be shy. See "Contacting the Carnassial Development Team" below for how to reach us.

If you'd like to translate Carnassial into your language it's easy. Send us your edited version of Resources.xaml or drop us a line and we'll
set you up.

If you're a developer and would like to submit a pull request please see below.

### Requirements
* Carnassial and the template editor are supported and tested on current releases of Windows 10.
* Carnassial and the template editor should also run on Windows Server 2008 or newer and legacy Windows 8.1, 8, and 7 SP1 systems not updated
to Windows 10. This isn't offically supported, though.
* Windows Vista SP2 and earlier and all 32 bit versions of Windows are not supported. Carnassial is 64 bit only and has minor reliance on Windows
7 common dialogs.

Screen sizes of 1600 x 900 or larger are recommended. Carnassial should run on any x64 processor but optimization effort generally targets 
hardware from the last five years.

Known limitations:

* Carnassial is developed primarily for midrange processors with the ability to execute four threads concurrently. Its algorithms also support
two concurrent threads and scale to six or more cores but may not perform optimally.
* Microsoft Windows does not report file times consistently at sub-millisecond precision. While it's not been observed, it's possible rounding 
within the operating system may cause rereading date times from files without metadata to change the millisecond component of timestamps.

Known limitations with earlier versions of Windows:

* Users may need to [install .NET 5.0 or newer](https://docs.microsoft.com/en-us/dotnet/framework/migration-guide/versions-and-dependencies) if 
it's not already present using, for example, the [.NET installer](https://dotnet.microsoft.com/download).
* Users may need to [install the Universal C Runtime](https://www.microsoft.com/en-us/download/details.aspx?id=48234) if it or the Microsoft 
Visual C++ 2015-2019 redistributable is not already installed.
* Recycle Bin integration is untested on Windows 7.

### History
Carnassial is named for [carnassials](https://en.wikipedia.org/wiki/Carnassial) as its function is analogous (though unfortunately it lacks 
the teeth's self-sharpening properties).

Carnassial began as a substantial overhaul of Timelapse 2.0 for improved code quality and sufficient flexibility to accomodate typical 
carnivore studies. [Timelapse 2.1](http://saul.cpsc.ucalgary.ca/timelapse/pmwiki.php?n=Main.HomePage) includes several months of the initial 
Carnassial coding effort but is now diverged.

### Alternatives
The need to analyze remote camera data is a common one. In addition to Carnassial and Timelapse we're aware of 
[CPW Photo Warehouse​](http://cpw.state.co.us/learn/Pages/ResearchMammalsSoftware.aspx) and [eMammal](http://emammal.si.edu/). Key differences are

* Carnassial is fully free whilst CPW Photo Warehouse​ requires a Microsoft Access license (and permissions configuration). Carnassial is more flexible and 
smoother in data entry but presently lacks equivalents to CPW's station information, occupancy analysis, and mark recapture analysis. 
[CritterShell](https://github.com/CascadesCarnivoreProject/CritterShell) offers detection and occupancy analysis.
* Carnassial is readily available. Obtaining the eMammal client requires a logon be issued, which can be hard to get.
* Carnassial and Timelapse are broadly similar. As of March 2017, Carnassial offered faster analysis, more flexibility, and fewer defects than Timelapse.
As of March 2021, episodes are the main Timelapse feature absent from Carnassial. Episodes are, however, equivalent to detections in CritterShell and
require only a [dplyr](https://r4ds.had.co.nz/) `mutate()` statement to implement in R.

If you know of other analysis tools please let us know.

### Contacting the Carnassial Development Team
Feel free to open new issues on Carnassial here on github. Or email us at carnassialdev@gmail.com.

### Development Environment
Install [Visual Studio 2019 Community](https://www.visualstudio.com/en-us/products/visual-studio-community-vs.aspx) or newer with the C#
and C++ desktop workloads (other Visual Studio SKUs such as Enterprise are fine). After Visual Studio installation:

* clone the repo locally through Visual Studio's Team Explorer or GitHub's clone or download options
* install the [WiX Toolset](http://wixtoolset.org/releases/) 3.14 or later and Visual Studio WiX extension

Commits should

* include appropriate test coverage
* have no build warnings or live code analysis messages

Application and test development is done against .NET 5.0. Carnassial is a 64 bit app and for the most part only an x64 build is needed
for development and testing (the installer automatically rewires itself to build x86 under an x64 build). However, the Visual Studio 
development UI is a 32 bit app and is therefore unable to load controls from the regular Carnassial build for display in the WPF designer. 
As a result, Carnassial has a vestigial x86 build which needs to be selected when doing UI tasks if the view in the designer is to match 
what's displayed at x64 runtime. (Building Carnassial as AnyCPU in the x64 build isn't an option as StockMessageControl hits call graphs 
which go into Carnassial.Native, an approach which is anyways undesirable as there's a moderate performance penalty to building AnyCPU 
rather than x64.)

Historically, Visual Studio's discovery and honoring of test.runsettings has been unreliable, requiring manual selection of x64 test execution.
In such situations VS can fail to find any unit tests until restarted, though setting x64 and forcing a build typically resulted in test 
discovery. This appears to be less of an issue in Visual Studio 2018.

Warning C4945 currently suppressed as it's raised erroneously in the Native project. This is a [known issue](https://docs.microsoft.com/en-us/dotnet/core/porting/cpp-cli).
with referencing the Windows Preseantation Foundation in .NET 5.0 C++/CLI builds.

Carnassial is not currently MVVM. In general, greater use of MVVM would be beneficial but current UX development effort is primarily directed
to model-view adoption in order to enable refactoring to view models. Carnassial uses WPF resource dictionaries for localization as the 
approach is lighter weight and more flexible than .resx files or locbaml type methods. Culture specific resources are merged in 
Carnassial\LocalizedApplication.cs

Also helpful are

* Atlassian's [https://www.sourcetreeapp.com/](SourceTree), a recommended augmentation of the git support available in Visual Studio's Team Explorer.
* [DB Browser for SQLite](http://sqlitebrowser.org/)
* The [Visual Studio Image Library](https://msdn.microsoft.com/en-us/library/ms246582.aspx) for icons.
* John Skeet's discussion of [DateTime, DateTimeOffset, and TimeZoneInfo limitations](http://blog.nodatime.org/2011/08/what-wrong-with-datetime-anyway.html).
* Microsoft's [terminology search](https://www.microsoft.com/en-us/language) for translations.


### Dependencies
* Visual Studio for development
* nuget packages as referenced by the solution
* libjpeg-turbo, which is managed manually as described below

A nuget package for libjpeg-turbo exists but is not being maintained so the library is comitted to the Carnassial repro. To update, download the [libjpeg-turbo](https://libjpeg-turbo.org/) VC and VC64 installers, copy the new bits to Native\libjpeg-turbo\{bin, include, lib}, git add -f them, and update additional dependencies in Native.vcxproj's linker input settings to point to the new .lib.

Carnassial needs MFC only for the version header of Carnassial.Native.dll. Another detail is app.rc must be Unicode as Microsoft's resource compiler does not support UTF-8. This is incompatible with git differencing's lack of Unicode support and is undesirable with respect to the de facto convention of using UTF-8 for source files.
