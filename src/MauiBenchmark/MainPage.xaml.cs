using System.Text;
using Benchmarks.Core;
using MauiBenchmark.Services;
using Microsoft.Maui.Storage;

namespace MauiBenchmark;

public partial class MainPage : ContentPage
{
	private readonly StringBuilder _log = new();
	private string? _collectionDbPath;
	private BenchmarkReport? _lastReport;
	private bool _running;

	public MainPage()
	{
		InitializeComponent();
		_collectionDbPath = PlatformSupport.CollectionDbPath;
		if (_collectionDbPath is not null)
		{
			CollectionPathLabel.Text = $"Collection.db: {_collectionDbPath}";
		}
	}

	private async void OnPickDbClicked(object? sender, EventArgs e)
	{
		try
		{
			var result = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Select Collection.db" });
			if (result is null)
			{
				return;
			}

			// Copy into app data so the engine can open it reliably.
			var dest = Path.Combine(PlatformSupport.DefaultDataDir, "Collection.db");
			using (var src = await result.OpenReadAsync())
			using (var dst = File.Create(dest))
			{
				await src.CopyToAsync(dst);
			}
			_collectionDbPath = dest;
			CollectionPathLabel.Text = $"Collection.db: {dest}";
		}
		catch (Exception ex)
		{
			await DisplayAlert("Pick failed", ex.Message, "OK");
		}
	}

	private async void OnRunClicked(object? sender, EventArgs e)
	{
		if (_running)
		{
			return;
		}
		_running = true;
		RunBtn.IsEnabled = false;
		ShareBtn.IsEnabled = false;
		Busy.IsRunning = true;
		_log.Clear();
		LogLabel.Text = string.Empty;
		ResultsLabel.Text = "(running…)";
		StatusLabel.Text = "Running…";

		var config = new BenchmarkConfig
		{
			Run1000 = Ds1000.IsChecked,
			Run1Million = Ds1M.IsChecked,
			RunCollection = DsCollection.IsChecked,
			UseLucene = EngLucene.IsChecked,
			UseFts5 = EngFts5.IsChecked,
			UseSqliteIndex = EngIndex.IsChecked,
			WorkRoot = PlatformSupport.WorkRoot,
			CollectionDbPath = _collectionDbPath,
		};

		try
		{
			var report = await BenchmarkAppRunner.RunAsync(config, AppendLog, CancellationToken.None);
			_lastReport = report;
			ResultsLabel.Text = BenchmarkRunner.ToMarkdown(report);
			BenchmarkAppRunner.WriteOutputs(report);
			StatusLabel.Text = "Done";
			ShareBtn.IsEnabled = true;
		}
		catch (Exception ex)
		{
			StatusLabel.Text = "Failed";
			ResultsLabel.Text = ex.ToString();
		}
		finally
		{
			Busy.IsRunning = false;
			RunBtn.IsEnabled = true;
			_running = false;
		}
	}

	private async void OnShareClicked(object? sender, EventArgs e)
	{
		if (_lastReport is null)
		{
			return;
		}
		try
		{
			var path = Path.Combine(PlatformSupport.OutputDirectory, BenchmarkHost.ResultsMarkdownFileName);
			await Share.Default.RequestAsync(new ShareFileRequest
			{
				Title = "FTS Benchmark results",
				File = new ShareFile(path),
			});
		}
		catch (Exception ex)
		{
			await DisplayAlert("Share failed", ex.Message, "OK");
		}
	}

	private void AppendLog(string message)
	{
		MainThread.BeginInvokeOnMainThread(() =>
		{
			_log.AppendLine(message);
			// Keep the log view from growing unbounded.
			const int maxChars = 8000;
			var text = _log.ToString();
			LogLabel.Text = text.Length > maxChars ? text[^maxChars..] : text;
			StatusLabel.Text = message.Length > 60 ? message[..60] : message;
		});
	}
}
