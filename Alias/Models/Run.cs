﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Validation;

namespace Alias.Models {
    public class Run : ReactiveObject, IDisposable {
        private static readonly Random Random = new Random();

#if DEBUG
        public static readonly TimeSpan RoundTime = TimeSpan.FromSeconds(10);
#else
        public static readonly TimeSpan RoundTime = TimeSpan.FromMinutes(1);
#endif
        private DateTimeOffset _startTime;

        private readonly List<string> _words;

        private CancellationTokenSource _cancellationTokenSource;

        public Player Player { get; }

        public Run(Player player, List<string> words) {
            Requires.NotNull(player, nameof(player));
            Requires.NotNull(words, nameof(words));

            Player = player;

            _words = words;
        }

        public void Dispose() {
            _cancellationTokenSource?.Dispose();
        }

        public async Task Start(CancellationToken cancellationToken) {
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            await Player.YesNoInteraction.Handle(Unit.Default).ToTask(_cancellationTokenSource.Token);

            IsRunning = true;
            _startTime = DateTimeOffset.Now;
            _cancellationTokenSource.CancelAfter(RoundTime);

            try {
                while (!_cancellationTokenSource.IsCancellationRequested && _words.Count > 0) {
                    Word = PeekRandomWord();

                    var accept = await Player.YesNoInteraction.Handle(Unit.Default).ToTask(_cancellationTokenSource.Token);
                    if (accept) {
                        _words.Remove(Word);

                        Score.HitCount++;
                    } else {
                        Score.MissCount++;
                    }
                }
            } catch (OperationCanceledException) {
                // ignored

            } finally {
                IsRunning = false;
            }
        }

        [Reactive]
        public bool IsRunning { get; set; }

        public TimeSpan TimeRemaining {
            get {
                if (!IsRunning)
                    return RoundTime;

                var remaining = DateTimeOffset.Now - _startTime;
                if (remaining.Ticks <= 0) {
                    remaining = TimeSpan.Zero;
                }

                return remaining;
            }
        }

        [Reactive]
        public string Word { get; set; }

        public Score Score { get; } = new Score();

        private string PeekRandomWord() {
            Requires.ValidState(_words.Any(), nameof(_words));

            // important. see below
            if (_words.Count == 1)
                return _words[0]; 

            var index = Random.Next(0, _words.Count);

            if (_words[index] == Word) {
                // true way of finding different but still random index
                var idx = Random.Next(0, _words.Count - 1);
                if (idx >= index)
                    idx++;

                index = idx;
            }

            return _words[index];
        }
    }
}
