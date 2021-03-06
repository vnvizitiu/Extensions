// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using Xunit;

namespace Microsoft.Extensions.Primitives
{
    public class ChangeTokenTests
    {
        public class TestChangeToken : IChangeToken
        {
            private Action _callback;

            public bool ActiveChangeCallbacks { get; set; }
            public bool HasChanged { get; set; }

            public IDisposable RegisterChangeCallback(Action<object> callback, object state)
            {
                _callback = () => callback(state);
                return null;
            }

            public void Changed()
            {
                HasChanged = true;
                _callback();
            }
        }

        [Fact]
        public void HasChangeFiresChange()
        {
            var token = new TestChangeToken();
            bool fired = false;
            ChangeToken.OnChange(() => token, () => fired = true);
            Assert.False(fired);
            token.Changed();
            Assert.True(fired);
        }

        [Fact]
        public void ChangesFireAfterExceptions()
        {
            TestChangeToken token = null;
            var count = 0;
            ChangeToken.OnChange(() => token = new TestChangeToken(), () =>
            {
                count++;
                throw new Exception();
            });
            Assert.Throws<Exception>(() => token.Changed());
            Assert.Equal(1, count);
            Assert.Throws<Exception>(() => token.Changed());
            Assert.Equal(2, count);
        }

        [Fact]
        public void HasChangeFiresChangeWithState()
        {
            var token = new TestChangeToken();
            object state = new object();
            object callbackState = null;
            ChangeToken.OnChange(() => token, s => callbackState = s, state);
            Assert.Null(callbackState);
            token.Changed();
            Assert.Equal(state, callbackState);
        }

        [Fact]
        public void ChangesFireAfterExceptionsWithState()
        {
            TestChangeToken token = null;
            var count = 0;
            object state = new object();
            object callbackState = null;
            ChangeToken.OnChange(() => token = new TestChangeToken(), s =>
            {
                callbackState = s;
                count++;
                throw new Exception();
            }, state);
            Assert.Throws<Exception>(() => token.Changed());
            Assert.Equal(1, count);
            Assert.NotNull(callbackState);
            Assert.Throws<Exception>(() => token.Changed());
            Assert.Equal(2, count);
            Assert.NotNull(callbackState);
        }

        [Fact]
        public void AsyncLocalsNotCapturedAndRestored()
        {
            // Capture clean context
            var executionContext = ExecutionContext.Capture();

            var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;
            var cancellationChangeToken = new CancellationChangeToken(cancellationToken);
            var executed = false;

            // Set AsyncLocal
            var asyncLocal = new AsyncLocal<int>();
            asyncLocal.Value = 1;

            // Register Callback
            cancellationChangeToken.RegisterChangeCallback(al =>
            {
                // AsyncLocal not set, when run on clean context
                // A suppressed flow runs in current context, rather than restoring the captured context
                Assert.Equal(0, ((AsyncLocal<int>) al).Value);
                executed = true;
            }, asyncLocal);

            // AsyncLocal should still be set
            Assert.Equal(1, asyncLocal.Value);

            // Check AsyncLocal is not restored by running on clean context
            ExecutionContext.Run(executionContext, cts => ((CancellationTokenSource)cts).Cancel(), cancellationTokenSource);

            // AsyncLocal should still be set
            Assert.Equal(1, asyncLocal.Value);
            Assert.True(executed);
        }
    }
}
