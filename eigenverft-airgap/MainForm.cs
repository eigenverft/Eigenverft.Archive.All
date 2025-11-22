using System;
using System.Threading;
using System.Threading.Tasks; // via Theraot.Core (net20)
using System.Windows.Forms;

// using Eigenverft.AirGap.Extensions.Common.Threading;  // no longer needed with AsyncFlow
using Eigenverft.AirGap;
using Eigenverft.AirGap.Extensions;

using Theraot; // AsyncFlow in same namespace/file set

namespace Eigenverft.AirGap
{

    public partial class MainForm : Form
    {
        private CancellationTokenSource _cts;
        private Task _runningTask;
        private Task _runningTask2;
        private SynchronizationContext _ui; // UI-Thread-Kontext
        private int _progressMax;

        /// <summary>
        /// Liefert Fenstererstellungsparameter (DropShadow).
        /// </summary>
        protected override CreateParams CreateParams
        {
            get
            {
                const int CS_DROPSHADOW = 0x20000;
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= CS_DROPSHADOW;
                return cp;
            }
        }

        /// <summary>
        /// Konstruktor: UI-Setup, Events, Cancellation.
        /// </summary>
        public MainForm()
        {
            InitializeComponent();

            this.AutoScaleMode = AutoScaleMode.None;
            this.AutoSize = false;

            MainPanel.EnableDrag(this.Handle);
            progressBarEx31.EnableDrag(this.Handle);
            transparentLabelExText.EnableDrag(this.Handle);

            _cts = new CancellationTokenSource();

            this.Shown += MainForm_Shown;
            this.FormClosing += MainForm_FormClosing;
            transparentLabelExText.Click += TransparentLabelClose_Click;
        }

        /// <summary>
        /// Erfasst UI-Kontext, initialisiert Ambient-UI und startet den Flow.
        /// </summary>
        private void MainForm_Shown(object sender, EventArgs e)
        {
            _ui = SynchronizationContext.Current ?? new SynchronizationContext();
            AsyncFlow.AmbientUi.Initialize(_ui); // optional UI-Posting in Workern

            StartBackgroundWorkflow();
        }

        /// <summary>
        /// Startet den cancel-fähigen Workflow mit UI-Finalisierung (Countdown).
        /// </summary>
        private void StartBackgroundWorkflow()
        {
            progressBarEx31.CustomText = "Starting...";
            _progressMax = progressBarEx31.Maximum;

            _runningTask =
                AsyncFlow
                    .Create(_ui)
                    .WithCancellation(_cts.Token)

                    // cancelable worker (bound to _cts)
                    .ThenAsync<int>(DoWorkAsync, _progressMax)

                    // uncancelable UI finalize (still runs; use DelayUncancelable inside)
                    .ThenUiAsync(FinalizeUiAsync)

                    .Run();

            _runningTask2 =
                AsyncFlow
                    .Create(_ui)
                    .WithCancellation(_cts.Token)
                    .ThenAsync(NextAsync)
                    .Run();

            
        }

        private Task NextAsync(CancellationToken ct)
        {
            
            Task seq = AsyncFlow.DelayUncancelable(10000);
            seq = AsyncFlow.ThenUi(seq,delegate
            {
                transparentLabelExText.Text = "Almost done...";
            });

            
           
            return Task.Delay(5000);
        }

        /// <summary>
        /// Allgemeine Arbeitsroutine (UI-agnostisch), Progress optional via Ambient-UI.
        /// </summary>
        /// <param name="max">Maximalwert.</param>
        /// <param name="ct">Kooperatives Abbruchtoken.</param>
        /// <returns>Task ohne Ergebnis.</returns>
        /// <remarks>Verwendet ThreadPool und TCS; UI-Updates erfolgen über <see cref="AsyncFlow.AmbientUi"/>.</remarks>
        /// <example>
        /// <code>
        /// // Eingebunden im Flow:
        /// // .ThenAsync&lt;int&gt;(DoWorkAsync, _progressMax)
        /// </code>
        /// </example>
        private Task DoWorkAsync(int max, CancellationToken ct)
        {
            TaskCompletionSource<object> tcs = new TaskCompletionSource<object>();

            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    int i = 0;
                    while (i <= max)
                    {
                        if (ct.IsCancellationRequested)
                        {
                            tcs.TrySetCanceled();
                            return;
                        }

                        Thread.Sleep(20); // simulierte Arbeit

                        int snapshot = i; // vermeiden von Capturing-Problemen
                        AsyncFlow.AmbientUi.Post(delegate
                        {
                            // Reviewer-Hinweis: UI-abhängige Properties existieren projektspezifisch (CustomText).
                            progressBarEx31.Value = snapshot;
                            progressBarEx31.CustomText = new ProgressUpdate(snapshot, max).ToString();
                        });

                        i = i + 1;
                    }
                    tcs.TrySetResult(null);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });

            return tcs.Task;
        }

        /// <summary>
        /// UI-Abschluss: zeigt Status (Error/Cancelled/Done) und schließt mit Countdown asynchron.
        /// </summary>
        /// <param name="completed">Vorgänger-Task für Statusinspektion.</param>
        /// <returns>Abschließender UI-Task.</returns>
        /// <example>
        /// <code>
        /// // Eingebunden im Flow:
        /// // .ThenUiAsync(FinalizeUiAsync)
        /// </code>
        /// </example>
        /// <summary>UI-Abschluss mit asynchronem Countdown – alle UI-Zugriffe strikt auf den UI-Thread.</summary>
        /// <summary>UI finalization with a safe async countdown (no cross-thread, no accidental skip).</summary>
        /// <remarks>Runs on UI via ThenUiAsync entry; every post-delay UI touch is marshalled back to UI.</remarks>
        /// <param name="completed">Preceding task to inspect.</param>
        /// <returns>Completion task.</returns>
        /// <summary>UI finalization with a real async countdown; worker cancel does NOT cancel the countdown.</summary>
        /// <param name="completed">Preceding task to inspect.</param>
        /// <returns>Completion task.</returns>
        private Task FinalizeUiAsync(Task completed)
        {
            string status =
                (completed == null) ? "Error."
              : completed.IsFaulted ? "Error."
              : completed.IsCanceled ? "Cancelled."
              : "Done.";

            // We are on UI (ThenUncancelableUiAsync), this first write is safe.
            progressBarEx31.CustomText = status + " Closing in 3...";

            // Build a sequential, fully-unwrapped chain.
            Task seq = AsyncFlow.DelayUncancelable(1000);
            seq = AsyncFlow.ThenUi(seq, delegate { progressBarEx31.CustomText = status + " Closing in 2..."; });
            seq = AsyncFlow.ThenDelayUncancelable(seq, 1000);
            seq = AsyncFlow.ThenUi(seq, delegate { progressBarEx31.CustomText = status + " Closing in 1..."; });
            seq = AsyncFlow.ThenDelayUncancelable(seq, 1000);
            seq = AsyncFlow.ThenUi(seq, delegate { this.Close(); });

            return seq; // the flow will wait until Close() UI action posts and runs
        }



        /// <summary>
        /// Benutzerabbruch (Klick): UI-Hinweis, ggf. harter Close wenn kein Task läuft, ansonsten Cancel.
        /// </summary>
        private void TransparentLabelClose_Click(object sender, EventArgs e)
        {
            Control ctl = sender as Control;
            if (ctl != null) ctl.Enabled = false;

            progressBarEx31.CustomText = "Cancelling...";

            if (_runningTask == null)
            {
                Thread.Sleep(1000);
                this.Close();
                return;
            }

            try { _cts.Cancel(); } catch { /* disposed tolerieren */ }
        }

        /// <summary>
        /// Beim Schließen Abbruch anfordern (idempotent).
        /// </summary>
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            try { _cts.Cancel(); } catch { }
        }
    }

    /// <summary>
    /// Immutable Fortschrittsdaten (Klasse).
    /// </summary>
    public sealed class ProgressUpdate
    {
        /// <summary>Aktueller Wert.</summary>
        public int Value { get; private set; }

        /// <summary>Maximalwert.</summary>
        public int Max { get; private set; }

        /// <summary>Erstellt eine neue Instanz.</summary>
        /// <param name="value">Aktueller Fortschritt.</param>
        /// <param name="max">Maximalwert.</param>
        public ProgressUpdate(int value, int max)
        {
            Value = value;
            Max = max;
        }

        /// <summary>Textdarstellung.</summary>
        /// <returns>Schema "Value/Max".</returns>
        public override string ToString()
        {
            return Value + "/" + Max;
        }
    }

}