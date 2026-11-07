using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using LibGit2Sharp;
using Memoria.Proofs;
using Renci.SshNet;

// The Windows proof: our SSH transport (LibGit2Sharp + SSH.NET) clones, commits, pushes, and
// fetches with both address forms, first against an in-process server, then against real hosts.
string home = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "Memoria-ToDo-proofs");
Action<string> log = Console.WriteLine;
SshTransport.Log = log;

try
{
    switch (args)
    {
        case ["keygen"]:
            ProofKey.Create();
            PrintKey(ProofKey.Load());
            return 0;
        case ["pubkey"]:
            PrintKey(ProofKey.Load());
            return 0;
        case ["local"]:
            await RunLocalAsync();
            return 0;
        case ["remote", string address]:
            RunRemote(SshAddress.Parse(address), ProofKey.Load().Key);
            return 0;
        case ["remote", string address, "--key", string keyPath]:
            RunRemote(SshAddress.Parse(address), OpenSshKey(keyPath));
            return 0;
        case ["report", string address, "--key", string keyPath]:
            PrintPhoneReports(SshAddress.Parse(address), OpenSshKey(keyPath));
            return 0;
        default:
            Console.Error.WriteLine("Usage: WindowsProof keygen | pubkey | local | remote <git@host:path or ssh://git@host/path> [--key <OpenSSH private key>] | report <address> --key <OpenSSH private key>");
            return 2;
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"FAILED: {ex}");
    return 1;
}

void PrintKey(ProofKey.Loaded key)
{
    log(key.PublicKeyLine);
    log($"SHA256:{key.Fingerprint}");
}

void PrintVersions()
{
    var version = GlobalSettings.Version;
    log($"{RuntimeInformation.FrameworkDescription}, {RuntimeInformation.ProcessArchitecture}");
    log($"LibGit2Sharp {version.InformationalVersion}, libgit2 {version.LibGit2CommitSha}, features: {version.Features}");
}

async Task RunLocalAsync()
{
    PrintVersions();
    var key = ProofKey.Load();
    string run = NewRunFolder("local");
    string served = Path.Combine(run, "served");
    SeedBareRepository(served, "memoria-proof.git");

    using var server = new TestSshServer(served, key.PublicKeyBlob, log);
    int port = await server.ListenAsync(IPAddress.Loopback, 0);
    await server.ListenAsync(IPAddress.Parse("127.0.0.2"), 22);
    log($"Test server: 127.0.0.1:{port} and 127.0.0.2:22, host key SHA256:{server.HostKeyFingerprint}");

    string urlForm = $"ssh://git@127.0.0.1:{port}/memoria-proof.git";
    var pins = new Dictionary<string, string[]>
    {
        ["127.0.0.1"] = [server.HostKeyFingerprint],
        ["127.0.0.2"] = [server.HostKeyFingerprint],
    };
    SshTransport.Use(key.Key, pins);
    GitScenario.Run(urlForm, "git@127.0.0.2:memoria-proof.git", Path.Combine(run, "clones"), log);
    GitScenario.RefuseUnpinnedHostKey(urlForm, "127.0.0.1", key.Key, log);

    log("All local checks passed.");
}

void RunRemote(SshAddress address, IPrivateKeySource key)
{
    PrintVersions();
    log($"Client key: {string.Join(", ", key.HostKeyAlgorithms.Select(a => a.Name))}");
    SshTransport.Use(key, PublishedHostKeys.Pins);
    GitScenario.Run(
        address.ToUrlForm(),
        address.ToScpForm(),
        Path.Combine(NewRunFolder(address.Host), "clones"),
        log);
    log($"All checks against {address.Host} passed.");
}

// Clones through our transport and prints the reports the phone proof pushed, newest last.
void PrintPhoneReports(SshAddress address, IPrivateKeySource key)
{
    SshTransport.Use(key, PublishedHostKeys.Pins);
    string clone = Path.Combine(NewRunFolder("report"), "clone");
    Repository.Clone(address.ToScpForm(), clone);
    string reports = Path.Combine(clone, "phone-reports");
    var files = Directory.Exists(reports) ? Directory.GetFiles(reports, "*.md").Order().ToArray() : [];
    if (files.Length == 0)
    {
        throw new InvalidOperationException($"{address.Host} has no phone reports yet.");
    }

    foreach (string file in files)
    {
        log($"===== {Path.GetFileName(file)}");
        log(File.ReadAllText(file));
    }
}

// An existing OpenSSH key; its passphrase, if any, comes only from the environment.
static PrivateKeyFile OpenSshKey(string path) =>
    new(path, Environment.GetEnvironmentVariable("MEMORIA_PROOF_KEY_PASSPHRASE"));

string NewRunFolder(string target)
{
    string folder = Path.Combine(home, "runs", $"{DateTime.Now:yyyyMMdd-HHmmss}-{target}");
    Directory.CreateDirectory(folder);
    log($"Run folder: {folder}");
    return folder;
}

void SeedBareRepository(string parent, string name)
{
    string seed = Path.Combine(parent, "seed");
    Directory.CreateDirectory(seed);
    Git(seed, "init", "-b", "main");
    File.WriteAllText(Path.Combine(seed, "README.md"), "# Memoria proof\n");
    Git(seed, "add", "README.md");
    Git(seed, "commit", "-m", "Seed the proof repository");
    Git(parent, "clone", "--bare", seed, name);
}

void Git(string workingDirectory, params string[] arguments)
{
    var start = new ProcessStartInfo("git")
    {
        WorkingDirectory = workingDirectory,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
    };
    foreach (string argument in (string[])["-c", "user.name=Memoria proof", "-c", "user.email=proof@memoria.invalid", .. arguments])
    {
        start.ArgumentList.Add(argument);
    }

    using var git = Process.Start(start)!;
    Task<string> output = git.StandardOutput.ReadToEndAsync();
    string error = git.StandardError.ReadToEnd();
    git.WaitForExit();
    _ = output.Result;
    if (git.ExitCode != 0)
    {
        throw new InvalidOperationException($"git {string.Join(' ', arguments)} exited with {git.ExitCode}: {error}");
    }
}
