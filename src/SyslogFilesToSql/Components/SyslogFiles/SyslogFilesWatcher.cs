﻿using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;

namespace SyslogFilesToSql.Components.SyslogFiles
{
    internal sealed class SyslogFilesWatcher : IObservable<FileInfo>, IDisposable
    {
        private readonly SyslogFilesWatcherOptions _options;

        private readonly FileSystemWatcher _fileWatcher;

        private readonly ILogger _logger;

        private readonly HashSet<IObserver<FileInfo>> _observers;

        private readonly Matcher _globMatcher;

        public SyslogFilesWatcher(IOptions<SyslogFilesWatcherOptions> options, ILogger<SyslogFilesWatcher> logger)
        {
            if (options is null)
                throw new ArgumentNullException(nameof(options));

            _logger = logger;

            _options = options.Value;
            _options.Validate();

            _fileWatcher = new FileSystemWatcher();
            _fileWatcher.Path = _options.SyslogDirectory!;
            _fileWatcher.NotifyFilter = NotifyFilters.FileName;
            _fileWatcher.Filter = "*";
            _fileWatcher.Changed += OnFileCreatedOrChanged;
            _fileWatcher.Renamed += OnFileRenamed;
            _fileWatcher.Created += OnFileCreatedOrChanged;
            _fileWatcher.Error += OnFileWatcherError;
            _fileWatcher.IncludeSubdirectories = false;

            _observers = new HashSet<IObserver<FileInfo>>(1);

            // If compression after import is enable, we will append extension ".gz" to files.
            _globMatcher = new Matcher();
            if (_options.SyslogFilePattern.EndsWith(".gz"))
                throw new ArgumentException($"{nameof(_options.SyslogFilePattern)} cannot end with '.gz' extension.");
            _globMatcher.AddIncludePatterns(new[] { _options.SyslogFilePattern });
            _globMatcher.AddExcludePatterns(new[] { "*.gz"});
        }

        public void Start()
        {
            _logger.LogInformation($"Watching syslog files in {_options.SyslogDirectory}...");

            foreach (string filepath in _globMatcher.GetResultsInFullPath(_options.SyslogDirectory!))
            {
                CheckFile(new FileInfo(filepath), checkFileGlobPattern: false);
            }

            _fileWatcher.EnableRaisingEvents = true;
        }

        private void OnFileWatcherError(object sender, ErrorEventArgs e)
        {
            Exception ex = e.GetException();
            _logger.LogError($"FileWatcher error: {ex}");
            foreach (var observer in _observers)
            {
                observer.OnError(ex);
            }
        }

        private void CheckFile(FileInfo file, bool checkFileGlobPattern)
        {
            if (checkFileGlobPattern)
            {
                PatternMatchingResult patternMatchResult = MatcherExtensions.Match(_globMatcher, _options.SyslogDirectory!, file.FullName);
                if (!patternMatchResult.HasMatches)
                {
                    return;
                }
            }
            
            if (!EnsureCanOpenFile(file))
            {
                return;
            }

            _logger.LogInformation($"Pushing file for processing: {file.Name}...");
            foreach (IObserver<FileInfo> observer in _observers)
            {
                try
                {
                    observer.OnNext(file);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error when pushing {file} to {observer.GetType().Name}.");
                }
            }

        }

        private bool EnsureCanOpenFile(FileInfo file)
        {
            if (!file.Exists)
            {
                return false;
            }

            return true;
        }
        
        private void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            _logger.LogInformation($"Renamed: {e.OldName} -> {e.Name}");
            CheckFile(new FileInfo(e.FullPath), checkFileGlobPattern: true);
        }

        private void OnFileCreatedOrChanged(object sender, FileSystemEventArgs e)
        {
            _logger.LogInformation($"{e.ChangeType}: {e.Name}");
            CheckFile(new FileInfo(e.FullPath), checkFileGlobPattern: true);
        }

        public void Dispose()
        {
            _fileWatcher.EnableRaisingEvents = false;
            // TODO: listen only for expected event (renamed?)
            _fileWatcher.Changed -= OnFileCreatedOrChanged;
            _fileWatcher.Created -= OnFileCreatedOrChanged;
            _fileWatcher.Renamed -= OnFileRenamed;
            _fileWatcher.Error -= OnFileWatcherError;
            _fileWatcher.Dispose();
            _logger.LogInformation($"Stopped syslog files watcher.");
        }

        public IDisposable Subscribe(IObserver<FileInfo> observer)
        {
            if (_observers.Add(observer))
            {
                _logger.LogInformation($"Subscriber registered: {observer.GetType().Name}");
            }
            return new Unsubscriber<FileInfo>(_observers, observer);
        }

        sealed class Unsubscriber<FileInfo> : IDisposable
        {
            private readonly ISet<IObserver<FileInfo>> _observers;
            private readonly IObserver<FileInfo> _observer;

            internal Unsubscriber(
                ISet<IObserver<FileInfo>> observers,
                IObserver<FileInfo> observer) => (_observers, _observer) = (observers, observer);

            public void Dispose() => _observers.Remove(_observer);
        }
    }
}
