﻿using System;
using System.Net.Http;
using System.Reactive.Linq;
using System.Threading.Tasks;
using MLS.Agent.DotnetCli;
using MLS.Agent.Tools;
using MLS.Agent.Workspaces;
using Pocket;
using WorkspaceServer.Models.Execution;
using static Pocket.Logger;

namespace WorkspaceServer.WorkspaceFeatures
{
    public class WebServer : IRunResultFeature, IDisposable
    {
        private readonly WorkspaceBuild workspaceBuild;
        private readonly AsyncLazy<HttpClient> _getHttpClient;
        private readonly AsyncLazy<Uri> _listeningAtUri;
        private readonly CompositeDisposable _disposables = new CompositeDisposable();

        public WebServer(WorkspaceBuild workspaceBuild)
        {
            this.workspaceBuild = workspaceBuild ?? throw new ArgumentNullException(nameof(workspaceBuild));

            _listeningAtUri = new AsyncLazy<Uri>(RunKestrel);

            _getHttpClient = new AsyncLazy<HttpClient>(async () =>
            {
                var httpClient = new HttpClient
                {
                    BaseAddress = await EnsureStarted()
                };

                return httpClient;
            });
        }

        private async Task<Uri> RunKestrel()
        {
            await workspaceBuild.EnsurePublished();

            var operation = Log.OnEnterAndExit();

            var process = CommandLine.StartProcess(
                DotnetMuxer.Path.FullName,
                workspaceBuild.EntryPointAssemblyPath.FullName,
                workspaceBuild.Directory,
                StandardOutput.OnNext,
                StandardError.OnNext,
                ("ASPNETCORE_DETAILEDERRORS", "1"),
                ("ASPNETCORE_URLS", $"http://127.0.0.1:0"),
                ("ASPNETCORE_PORT", null));

            _disposables.Add(() =>
            {
                operation.Dispose();
                process.Kill();
            });

            _disposables.Add(StandardOutput.Subscribe(s => operation.Trace(s)));
            _disposables.Add(StandardError.Subscribe(s => operation.Error(s)));

            var kestrelListeningMessagePrefix = "Now listening on: ";

            var uriString = await StandardOutput
                                  .Where(line => line.StartsWith(kestrelListeningMessagePrefix))
                                  .Select(line => line.Replace(kestrelListeningMessagePrefix, ""))
                                  .FirstAsync();

            operation.Trace("Starting Kestrel at {uri}.", uriString);

            return new Uri(uriString);
        }

        public StandardOutput StandardOutput { get; } = new StandardOutput();

        public StandardError StandardError { get; } = new StandardError();

        public Task<Uri> EnsureStarted() => _listeningAtUri.ValueAsync();

        public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
        {
            var httpClient = await _getHttpClient.ValueAsync();

            var response = await httpClient.SendAsync(request);

            return response;
        }

        public void Dispose() => _disposables.Dispose();

        public void Apply(RunResult runResult)
        {
        }
    }
}