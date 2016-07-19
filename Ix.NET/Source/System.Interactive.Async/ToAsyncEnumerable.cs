﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information. 
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

namespace System.Linq
{
    public static partial class AsyncEnumerable
    {
        public static IAsyncEnumerable<TSource> ToAsyncEnumerable<TSource>(this IEnumerable<TSource> source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            return Create(() =>
            {
                var e = source.GetEnumerator();

                return Create(
                    ct =>
                    {
                        var tcs = new TaskCompletionSource<bool>();
                        ct.Register(() => tcs.TrySetCanceled());

                        Task.Run(() =>
                        {
                            var res = false;
                            try
                            {
                                res = e.MoveNext();
                                tcs.TrySetResult(res);
                            }
                            catch (Exception ex)
                            {
                                tcs.TrySetException(ex);
                            }
                            finally
                            {
                                if (!res)
                                    e.Dispose();
                            }
                        }, ct);

                        return tcs.Task;
                    },
                    () => e.Current,
                    () => e.Dispose()
                );
            });
        }

        public static IEnumerable<TSource> ToEnumerable<TSource>(this IAsyncEnumerable<TSource> source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            return ToEnumerable_(source);
        }

        private static IEnumerable<TSource> ToEnumerable_<TSource>(IAsyncEnumerable<TSource> source)
        {
            using (var e = source.GetEnumerator())
            {
                while (true)
                {
                    if (!e.MoveNext(CancellationToken.None).Result)
                        break;
                    var c = e.Current;
                    yield return c;
                }
            }
        }

        public static IAsyncEnumerable<TSource> ToAsyncEnumerable<TSource>(this Task<TSource> task)
        {
            if (task == null)
                throw new ArgumentNullException(nameof(task));
            
            return Create(() =>
            {
                var called = 0;

                var value = default(TSource);
                return Create(
                    async ct =>
                    {
                        if (Interlocked.CompareExchange(ref called, 1, 0) == 0)
                        {
                            value = await task.ConfigureAwait(false);
                            return true;
                        }
                        return false;
                    },
                    () => value,
                    () => { });
            });
        }


    }
}
