using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;

[assembly: AssemblyTitle("Carnassial Template Editor")]
[assembly: AssemblyDescription("Editor for Carnassial template database (.tdb) files.")]
[assembly: AssemblyCompany("Cascades Carnivore Project")]
[assembly: AssemblyProduct("Carnassial Template Editor")]
[assembly: AssemblyCopyright("Copyright © 2016-2021 Cascades Carnivore Project")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

[assembly: AssemblyVersion("2.2.4.0")]
[assembly: AssemblyFileVersion("2.2.4.0")]

#if DEBUG
[assembly: AssemblyConfiguration("Debug")]
#else
[assembly: AssemblyConfiguration("Release")]
#endif

[assembly: ComVisible(false)]
[assembly: Guid("2cdd91eb-a708-4baa-a698-4ef49c021fc6")]

[assembly: InternalsVisibleTo("Carnassial.UnitTests")]

////                 no theme support                 styling is in this assembly
[assembly: ThemeInfo(ResourceDictionaryLocation.None, ResourceDictionaryLocation.SourceAssembly)]

[assembly: NeutralResourcesLanguage("en", UltimateResourceFallbackLocation.MainAssembly)]