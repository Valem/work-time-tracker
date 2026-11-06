using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Xml.Linq;

namespace Wtt.Architecture.Tests;

/// <summary>
/// The dependency view of concept §3: Domain depends on nothing, Application on Domain; Infrastructure and
/// Wtt.PublicHolidays implement Application's ports without knowing each other; the hosts — App, Recorder and the
/// developer CLI — compose them without knowing each other (Recorder and App meet only in the database), and the Recorder
/// composes without Wtt.PublicHolidays. The build-time BridgeTypes reads the App's bridge contract and nothing else.
/// Domain and Application reference neither Windows nor I/O, and no project but Wtt.PublicHolidays uses the network.
/// </summary>
public sealed class DependencyViewTests
{
    private const string NetworkProject = "Wtt.PublicHolidays";

    private static readonly string[] AdapterReferences = ["Wtt.Application", "Wtt.Domain"];

    private static readonly string[] RecorderReferences = ["Wtt.Infrastructure", .. AdapterReferences];

    private static readonly string[] HostReferences = [.. RecorderReferences, NetworkProject];

    private static readonly Dictionary<string, string[]> AllowedReferences = new()
    {
        ["Wtt.Domain"] = [],
        ["Wtt.Application"] = ["Wtt.Domain"],
        ["Wtt.Infrastructure"] = AdapterReferences,
        [NetworkProject] = AdapterReferences,
        ["Wtt.App"] = HostReferences,
        ["Wtt.Recorder"] = RecorderReferences,
        ["Wtt.Cli"] = HostReferences,
        ["Wtt.BridgeTypes"] = ["Wtt.App"],
    };

    /// <summary>
    /// Beyond the namespaces below, Application may hold a cancellation token and a lock for the public-holiday loads it
    /// runs in the background — and nothing else from System.Threading.
    /// </summary>
    private static readonly string[] ApplicationAdmits = ["System.Threading.CancellationToken", "System.Threading.Lock"];

    /// <summary>
    /// The only namespaces Domain and Application may use from outside themselves. Anything else — files, streams, network,
    /// registry, security, interop, Windows — fails until it is placed here on purpose.
    /// </summary>
    private static readonly string[] AllowedNamespaces =
    [
        "System",
        "System.Collections",
        "System.Collections.Generic",
        "System.Globalization",
        "System.Linq",
        "System.Runtime.CompilerServices",
        "System.Text",
        "System.Threading.Tasks",
    ];

    /// <summary>Namespaces of which only attributes may be used: debugger, assembly and target-framework metadata.</summary>
    private static readonly string[] AttributeOnlyNamespaces = ["System.Diagnostics", "System.Reflection", "System.Runtime.Versioning"];

    /// <summary>
    /// Inside the allowed namespaces: the machine's environment, the console, the process's domain, creating types by
    /// name, and marks of a platform-only API.
    /// </summary>
    private static readonly string[] DeniedTypePrefixes =
    [
        "System.Environment",
        "System.AppContext",
        "System.AppDomain",
        "System.Activator",
        "System.Console",
        "System.Runtime.Versioning.SupportedOSPlatformAttribute",
        "System.Runtime.Versioning.TargetPlatformAttribute",
    ];

    /// <summary>
    /// Members that read the machine's clock or time zone — time comes only from an injected provider, and a day is never
    /// taken at the machine's current time zone — or that reach a type by name, around the checks above.
    /// </summary>
    private static readonly string[] DeniedMembers =
    [
        "System.DateTime.get_Now",
        "System.DateTime.get_Today",
        "System.DateTime.ToLocalTime",
        "System.DateTimeOffset.get_Now",
        "System.DateTimeOffset.ToLocalTime",
        "System.DateTimeOffset.get_LocalDateTime",
        "System.DateTimeOffset.op_Implicit",
        "System.TimeProvider.get_System",
        "System.TimeZoneInfo.get_Local",
        "System.TimeZoneInfo.FindSystemTimeZoneById",
        "System.TimeZoneInfo.GetSystemTimeZones",
        "System.Type.GetType",
        "System.Type.GetTypeFromProgID",
        "System.Type.GetTypeFromCLSID",
        "System.Delegate.CreateDelegate",
    ];

    [Fact]
    public void Architecture_dependency_view_holds_and_domain_and_application_reference_neither_windows_nor_io()
    {
        ProbeTheChecksReportViolatingSamples();

        Assert.Empty(ReferenceViolations(ProjectReferences()));
        Assert.Empty(ForbiddenUses(Usage.Of(typeof(Wtt.Domain.Facts.ActivityFact).Assembly)));
        Assert.Empty(ForbiddenUses(Usage.Of(typeof(Wtt.Application.Ports.IFactJournal).Assembly), ApplicationAdmits));
        Assert.Empty(NetworkUsesOutsideTheNetworkProject(BuiltAssemblies()));
    }

    /// <summary>A check that cannot see a violation proves nothing by passing, so each first meets one it must report.</summary>
    private static void ProbeTheChecksReportViolatingSamples()
    {
        var crafted = new Dictionary<string, IReadOnlyList<string>>
        {
            ["Wtt.Domain"] = ["Wtt.Infrastructure"],
            ["Wtt.Application"] = ["Wtt.Domain", "Wtt.App"],
            ["Wtt.App"] = ["Wtt.Infrastructure", "Wtt.Recorder", "Wtt.BridgeTypes", NetworkProject],
            ["Wtt.BridgeTypes"] = ["Wtt.App", "Wtt.Infrastructure"],
            ["Wtt.Recorder"] = ["Wtt.Infrastructure", NetworkProject],
            ["Wtt.Infrastructure"] = ["Wtt.Application", NetworkProject],
            [NetworkProject] = ["Wtt.Application", "Wtt.Infrastructure"],
            ["Wtt.Unplaced"] = [],
        };
        Assert.Equal(
            [
                "Wtt.Domain → Wtt.Infrastructure", "Wtt.Application → Wtt.App", "Wtt.App → Wtt.Recorder", "Wtt.App → Wtt.BridgeTypes",
                "Wtt.BridgeTypes → Wtt.Infrastructure", "Wtt.Recorder → Wtt.PublicHolidays", "Wtt.Infrastructure → Wtt.PublicHolidays",
                "Wtt.PublicHolidays → Wtt.Infrastructure", "Wtt.Unplaced has no place in the dependency view",
            ],
            ReferenceViolations(crafted));

        var networkProbe = new Usage(
            Types:
            [
                "System.Net.Http.HttpClient", "System.Net.Sockets.Socket", "System.Net.WebUtility", "System.Net.Dns",
                "System.Net.Http.Headers.MediaTypeHeaderValue+Inner", "System.Uri", "System.Text.Json.JsonDocument", "System.Networking",
            ],
            Members: [],
            PInvokeMethods: []);
        Assert.Equal(
            [
                "Wtt.Cli uses System.Net.Http.HttpClient", "Wtt.Cli uses System.Net.Sockets.Socket", "Wtt.Cli uses System.Net.WebUtility",
                "Wtt.Cli uses System.Net.Dns", "Wtt.Cli uses System.Net.Http.Headers.MediaTypeHeaderValue+Inner",
            ],
            NetworkUsesOutsideTheNetworkProject(new Dictionary<string, Usage> { ["Wtt.Cli"] = networkProbe, [NetworkProject] = networkProbe }));
        Assert.Contains(
            "Wtt.PublicHolidays no longer shows the network use it exists for — the check may be blind",
            NetworkUsesOutsideTheNetworkProject(new Dictionary<string, Usage> { [NetworkProject] = new([], [], []) }));

        string[] threading =
        [
            "System.Threading.CancellationToken", "System.Threading.Lock", "System.Threading.Lock+Scope", "System.Threading.CancellationTokenSource",
            "System.Threading.Thread", "System.Threading.Mutex", "System.Threading.Timer", "System.Threading.LockCookie", "System.Threading.Tasks.Task",
        ];
        var threadingProbe = new Usage(threading, Members: [], PInvokeMethods: []);
        string[] neverAdmitted =
        [
            "uses System.Threading.CancellationTokenSource", "uses System.Threading.Thread", "uses System.Threading.Mutex", "uses System.Threading.Timer",
            "uses System.Threading.LockCookie",
        ];
        Assert.Equal(neverAdmitted, ForbiddenUses(threadingProbe, ApplicationAdmits));
        Assert.Equal(
            ["uses System.Threading.CancellationToken", "uses System.Threading.Lock", "uses System.Threading.Lock+Scope", .. neverAdmitted],
            ForbiddenUses(threadingProbe));

        string[] violating =
        [
            "System.IO.File", "System.IO.Stream", "System.Net.Http.HttpClient", "Microsoft.Win32.Registry", "System.Diagnostics.Process",
            "System.Diagnostics.Eventing.Reader.EventLogReader", "Microsoft.Data.Sqlite.SqliteConnection", "System.Windows.Forms.Form",
            "System.Runtime.InteropServices.SafeHandle", "System.Runtime.Versioning.SupportedOSPlatformAttribute", "System.Console",
            "System.AppContext",
        ];
        string[] harmless =
        [
            "System.TimeProvider", "System.TimeZoneInfo", "System.Collections.Generic.List`1", "System.Diagnostics.DebuggableAttribute",
            "System.Diagnostics.DebuggableAttribute+DebuggingModes", "System.Reflection.AssemblyCompanyAttribute",
            "System.Runtime.Versioning.TargetFrameworkAttribute", "System.Runtime.CompilerServices.AsyncTaskMethodBuilder",
            "Wtt.Domain.Facts.ActivityFact",
        ];
        var sample = new Usage([.. violating, .. harmless], Members: ["System.TimeZoneInfo.ConvertTime"], PInvokeMethods: ["Native.GetTickCount64"]);
        Assert.Equal([.. violating.Select(type => $"uses {type}"), "calls native code: Native.GetTickCount64"], ForbiddenUses(sample));

        var reviewersProbe = new Usage(
            Types:
            [
                "System.Diagnostics.FileVersionInfo", "System.Environment", "System.Environment+SpecialFolder", "System.OperatingSystem",
                "System.Security.Principal.WindowsIdentity", "System.TimeZoneInfo",
            ],
            Members:
            [
                "System.Diagnostics.FileVersionInfo.GetVersionInfo", "System.Environment.GetEnvironmentVariable", "System.Environment.GetFolderPath",
                "System.OperatingSystem.IsWindows", "System.Security.Principal.WindowsIdentity.GetCurrent", "System.TimeZoneInfo.get_Local",
            ],
            PInvokeMethods: []);
        Assert.Equal(
            [
                "uses System.Diagnostics.FileVersionInfo", "uses System.Environment", "uses System.Environment+SpecialFolder",
                "uses System.Security.Principal.WindowsIdentity", "uses System.TimeZoneInfo.get_Local",
            ],
            ForbiddenUses(reviewersProbe));

        string[] allowedMembers =
        [
            "System.TimeProvider.get_LocalTimeZone", "System.TimeProvider.GetLocalNow", "System.TimeProvider.GetUtcNow",
            "System.TimeZoneInfo.ConvertTime", "System.Type.GetTypeFromHandle", "System.Type.op_Equality",
        ];
        var clockAndReflectionProbe = new Usage(
            Types:
            [
                "System.Activator", "System.AppDomain", "System.DateTime", "System.DateTimeOffset", "System.Delegate", "System.OperatingSystem",
                "System.TimeProvider", "System.TimeZoneInfo", "System.Type",
            ],
            Members: [.. DeniedMembers, .. allowedMembers],
            PInvokeMethods: []);
        Assert.Equal(
            ["uses System.Activator", "uses System.AppDomain", .. DeniedMembers.Select(member => $"uses {member}")],
            ForbiddenUses(clockAndReflectionProbe));

        var infrastructure = ForbiddenUses(Usage.Of(typeof(Wtt.Infrastructure.DataFolder).Assembly));
        Assert.Contains(infrastructure, use => use.StartsWith("uses Microsoft.Data.Sqlite.", StringComparison.Ordinal));
        Assert.Contains(infrastructure, use => use.StartsWith("uses System.IO.", StringComparison.Ordinal));
        Assert.Contains(infrastructure, use => use.StartsWith("uses System.Diagnostics.Eventing.Reader.", StringComparison.Ordinal));
        Assert.Contains("uses System.Runtime.Versioning.SupportedOSPlatformAttribute", infrastructure);
        Assert.Contains(infrastructure, use => use.StartsWith("calls native code: ", StringComparison.Ordinal));
    }

    private static List<string> ReferenceViolations(IReadOnlyDictionary<string, IReadOnlyList<string>> references)
    {
        var violations = new List<string>();
        foreach (var (project, referenced) in references)
        {
            if (!AllowedReferences.TryGetValue(project, out var allowed))
            {
                violations.Add($"{project} has no place in the dependency view");
                continue;
            }

            violations.AddRange(referenced.Where(reference => !allowed.Contains(reference)).Select(reference => $"{project} → {reference}"));
        }

        return violations;
    }

    private static List<string> ForbiddenUses(Usage usage, IReadOnlyList<string>? admittedTypes = null) =>
    [
        .. usage.Types.Where(type => !IsAllowed(type, admittedTypes ?? [])).Select(type => $"uses {type}"),
        .. usage.Members.Where(DeniedMembers.Contains).Select(member => $"uses {member}"),
        .. usage.PInvokeMethods.Select(method => $"calls native code: {method}"),
    ];

    private static bool IsAllowed(string type, IReadOnlyList<string> admittedTypes)
    {
        var topLevel = type.Split('+')[0];
        var ns = Namespace(type);
        if (DeniedTypePrefixes.Any(prefix => topLevel.StartsWith(prefix, StringComparison.Ordinal)))
        {
            return false;
        }

        if (ns == "Wtt.Domain" || ns.StartsWith("Wtt.Domain.", StringComparison.Ordinal) || admittedTypes.Contains(topLevel))
        {
            return true;
        }

        return AttributeOnlyNamespaces.Contains(ns)
            ? topLevel.EndsWith("Attribute", StringComparison.Ordinal)
            : AllowedNamespaces.Contains(ns);
    }

    /// <summary>
    /// Every use of System.Net by a project but <see cref="NetworkProject"/> — and that project's own use, which must show,
    /// or the check could not see one.
    /// </summary>
    private static List<string> NetworkUsesOutsideTheNetworkProject(IReadOnlyDictionary<string, Usage> assemblies)
    {
        var violations = new List<string>();
        foreach (var (project, usage) in assemblies)
        {
            var network = usage.Types.Where(type => Namespace(type) is "System.Net" || Namespace(type).StartsWith("System.Net.", StringComparison.Ordinal)).ToList();
            if (project != NetworkProject)
            {
                violations.AddRange(network.Select(type => $"{project} uses {type}"));
            }
            else if (network.Count == 0)
            {
                violations.Add($"{project} no longer shows the network use it exists for — the check may be blind");
            }
        }

        return violations;
    }

    private static string Namespace(string type)
    {
        var topLevel = type.Split('+')[0];
        return topLevel.Contains('.') ? topLevel[..topLevel.LastIndexOf('.')] : string.Empty;
    }

    /// <summary>
    /// What each source project's compiled assembly uses, read from its build output of this test's configuration — the
    /// hosts are executables this test cannot reference, and the project file names each assembly and framework.
    /// </summary>
    private static Dictionary<string, Usage> BuiltAssemblies()
    {
        var root = RepositoryRoot();
        var configuration = new DirectoryInfo(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory)).Parent!.Name;
        var defaultFramework = XDocument.Load(Path.Combine(root, "Directory.Build.props")).Descendants("TargetFramework").Single().Value;
        var assemblies = new Dictionary<string, Usage>();
        foreach (var folder in Directory.GetDirectories(Path.Combine(root, "src")))
        {
            var name = Path.GetFileName(folder);
            var projectFile = Path.Combine(folder, name + ".csproj");
            if (!File.Exists(projectFile))
            {
                continue;
            }

            var project = XDocument.Load(projectFile);
            var assemblyName = project.Descendants("AssemblyName").SingleOrDefault()?.Value ?? name;
            var framework = project.Descendants("TargetFramework").SingleOrDefault()?.Value ?? defaultFramework;
            var assembly = Path.Combine(folder, "bin", configuration, framework, assemblyName + ".dll");
            Assert.True(File.Exists(assembly), $"{name} is not built at {assembly}: build the solution first.");
            assemblies[name] = Usage.Of(assembly);
        }

        return assemblies;
    }

    /// <summary>Each source project's direct project references, read from its project file.</summary>
    private static Dictionary<string, IReadOnlyList<string>> ProjectReferences()
    {
        var src = Path.Combine(RepositoryRoot(), "src");
        return Directory.GetDirectories(src)
            .Select(folder => Path.Combine(folder, Path.GetFileName(folder) + ".csproj"))
            .Where(File.Exists)
            .ToDictionary(
                project => Path.GetFileNameWithoutExtension(project),
                project => (IReadOnlyList<string>)XDocument.Load(project)
                    .Descendants("ProjectReference")
                    .Select(reference => Path.GetFileNameWithoutExtension(reference.Attribute("Include")!.Value.Replace('\\', '/')))
                    .ToList());
    }

    private static string RepositoryRoot()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "WorkTimeTracker.slnx")))
            {
                return folder.FullName;
            }
        }

        throw new InvalidOperationException("WorkTimeTracker.slnx not found above the test's folder.");
    }

    /// <summary>What a compiled assembly uses from outside itself — types and their members — and the methods it implements in native code.</summary>
    private sealed record Usage(IReadOnlyList<string> Types, IReadOnlyList<string> Members, IReadOnlyList<string> PInvokeMethods)
    {
        public static Usage Of(Assembly assembly) => Of(assembly.Location);

        public static Usage Of(string assemblyFile)
        {
            using var stream = File.OpenRead(assemblyFile);
            using var image = new PEReader(stream);
            var metadata = image.GetMetadataReader();

            var types = metadata.TypeReferences.Select(handle => FullName(metadata, handle)).Distinct().Order(StringComparer.Ordinal).ToList();
            var members = metadata.MemberReferences
                .Select(metadata.GetMemberReference)
                .Where(member => member.Parent.Kind == HandleKind.TypeReference)
                .Select(member => $"{FullName(metadata, (TypeReferenceHandle)member.Parent)}.{metadata.GetString(member.Name)}")
                .Distinct()
                .Order(StringComparer.Ordinal)
                .ToList();
            var native = metadata.TypeDefinitions
                .Select(metadata.GetTypeDefinition)
                .SelectMany(type => type.GetMethods().Select(metadata.GetMethodDefinition).Select(method => (type, method)))
                .Where(entry => (entry.method.Attributes & MethodAttributes.PinvokeImpl) != 0)
                .Select(entry => $"{metadata.GetString(entry.type.Name)}.{metadata.GetString(entry.method.Name)}")
                .Order(StringComparer.Ordinal)
                .ToList();
            return new Usage(types, members, native);
        }

        private static string FullName(MetadataReader metadata, TypeReferenceHandle handle)
        {
            var type = metadata.GetTypeReference(handle);
            var name = metadata.GetString(type.Name);
            if (type.ResolutionScope.Kind == HandleKind.TypeReference)
            {
                return $"{FullName(metadata, (TypeReferenceHandle)type.ResolutionScope)}+{name}";
            }

            var ns = metadata.GetString(type.Namespace);
            return ns.Length == 0 ? name : $"{ns}.{name}";
        }
    }
}
