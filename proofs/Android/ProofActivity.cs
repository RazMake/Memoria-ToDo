using System.Diagnostics;
using System.Globalization;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Views;
using AndroidX.AppCompat.App;

namespace Memoria.Proofs;

/// <summary>The proof screen: creates the Keystore key, shows its public key, runs the proof, and shows the report.</summary>
[Activity(Label = "Proof Run", Exported = true, TaskAffinity = "com.razmake.memoriaproof.run")]
[IntentFilter([Intent.ActionMain], Categories = [Intent.CategoryLauncher])]
public sealed class ProofActivity : AppCompatActivity
{
    private TextView status = null!;
    private TextView keyText = null!;
    private Button createKey = null!;
    private Button copyKey = null!;
    private Button shareKey = null!;
    private EditText address = null!;
    private Button run = null!;
    private Button copyReport = null!;
    private Button shareReport = null!;
    private TextView log = null!;
    private KeystoreRsaKey? key;
    private string? report;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        LaunchTimings.ClaimColdStart();
        base.OnCreate(savedInstanceState);

        var column = new LinearLayout(this) { Orientation = Orientation.Vertical };
        int pad = (int)(16 * Resources!.DisplayMetrics!.Density);
        column.SetPadding(pad, pad, pad, pad);
        status = Add(column, new TextView(this) { TextSize = 18 });
        keyText = Add(column, new TextView(this) { TextSize = 12 });
        keyText.SetTextIsSelectable(true);
        createKey = Add(column, new Button(this) { Text = "Create the SSH key in the Keystore" });
        copyKey = Add(column, new Button(this) { Text = "Copy the public key" });
        shareKey = Add(column, new Button(this) { Text = "Share the public key" });
        address = Add(column, new EditText(this) { Text = PhoneProof.DefaultAddress, Hint = "Repository address" });
        address.SetSingleLine(true);
        run = Add(column, new Button(this) { Text = "Run the proof" });
        copyReport = Add(column, new Button(this) { Text = "Copy the report" });
        shareReport = Add(column, new Button(this) { Text = "Share the report" });
        log = Add(column, new TextView(this) { TextSize = 11, Typeface = Typeface.Monospace });
        log.SetTextIsSelectable(true);

        var scroll = new ScrollView(this);
        scroll.SetFitsSystemWindows(true);
        scroll.AddView(column);
        SetContentView(scroll);

        createKey.Click += async (_, _) => await CreateKeyAsync();
        copyKey.Click += (_, _) => Copy("SSH public key", key!.PublicKeyLine);
        shareKey.Click += (_, _) => Share(key!.PublicKeyLine);
        run.Click += async (_, _) => await RunAsync();
        copyReport.Click += (_, _) => Copy("Phone proof report", report!);
        shareReport.Click += (_, _) => Share(report!);

        key = KeystoreRsaKey.TryLoad();
        Refresh(key is null ? "Create the key first." : "Add the public key to the repository, then run the proof.");
    }

    private async Task CreateKeyAsync()
    {
        createKey.Enabled = false;
        status.Text = "Creating an RSA 3072 key in the Keystore. This can take a while.";
        try
        {
            long started = Stopwatch.GetTimestamp();
            key = await Task.Run(KeystoreRsaKey.Create);
            string took = Stopwatch.GetElapsedTime(started).TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture);
            File.WriteAllText(PhoneProof.KeyCreationFile(this), took);
            Refresh($"Key created in {took} ms. Add the public key to the repository, then run the proof.");
        }
        catch (Exception ex)
        {
            Refresh($"Creating the key failed: {ex.Message}");
            log.Text = ex.ToString();
        }
    }

    private async Task RunAsync()
    {
        run.Enabled = false;
        log.Text = "";
        status.Text = "Running the proof.";
        string target = address.Text ?? PhoneProof.DefaultAddress;
        var result = await Task.Run(() => PhoneProof.Run(this, key!, target, line => RunOnUiThread(() => log.Append(line + "\n"))));
        report = result.Report;
        string outcome = result.Passed ? "The proof passed." : "The proof FAILED.";
        Refresh(result.PushedAs is null
            ? $"{outcome} The report wasn't pushed: copy or share it."
            : $"{outcome} The report is in the repository as {result.PushedAs}.");
    }

    private void Refresh(string message)
    {
        status.Text = message;
        keyText.Text = key is null ? "" : $"{key.PublicKeyLine}\n\nSHA256:{key.Fingerprint}";
        createKey.Enabled = key is null;
        copyKey.Enabled = shareKey.Enabled = run.Enabled = key is not null;
        copyReport.Enabled = shareReport.Enabled = report is not null;
    }

    private void Copy(string label, string text)
    {
        var clipboard = (ClipboardManager)GetSystemService(ClipboardService)!;
        clipboard.PrimaryClip = ClipData.NewPlainText(label, text);
        Toast.MakeText(this, "Copied.", ToastLength.Short)!.Show();
    }

    private void Share(string text)
    {
        var send = new Intent(Intent.ActionSend);
        send.SetType("text/plain");
        send.PutExtra(Intent.ExtraText, text);
        StartActivity(Intent.CreateChooser(send, "Share"));
    }

    private static T Add<T>(LinearLayout column, T view)
        where T : View
    {
        column.AddView(view, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));
        return view;
    }
}
