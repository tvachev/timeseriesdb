#region COPYRIGHT

/*
 *     Copyright 2009-2012 Yuri Astrakhan  (<Firstname><Lastname>@gmail.com)
 *
 *     This file is part of FastBinTimeseries library
 * 
 *  FastBinTimeseries is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 * 
 *  FastBinTimeseries is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 * 
 *  You should have received a copy of the GNU General Public License
 *  along with FastBinTimeseries.  If not, see <http://www.gnu.org/licenses/>.
 *
 */

#endregion

using System;
using System.Collections.Generic;
using System.Globalization;
using NYurik.FastBinTimeseries.Serializers;
using NYurik.FastBinTimeseries.Serializers.BlockSerializer;

namespace NYurik.FastBinTimeseries.Test.BlockSerializer
{
    public class SerializtionTestsBase : TestsBase
    {
        protected IEnumerable<T> Values<T>(Func<long, T> converter, long min = long.MinValue, long max = long.MaxValue)
        {
            foreach (long i in StreamCodecTests.TestValuesGenerator())
                if (i >= min && i <= max)
                    yield return converter(i);
        }

        protected IEnumerable<T> Range<T>(T min, T max, Func<T, T> inc)
            where T : IComparable<T>
        {
            T val = min;
            yield return val;

            while (val.CompareTo(max) < 0)
            {
                val = inc(val);
                yield return val;
            }
        }


        protected void Run<T>(IEnumerable<T> values, string name = null,
                              Action<BaseField> updateSrlzr = null, Func<T, T, bool> comparer = null)
        {
            using (var codec = new CodecWriter(10000))
            {
                DynamicSerializer<T> ds = DynamicSerializer<T>.CreateDefault();

                try
                {
                    if (updateSrlzr != null)
                        updateSrlzr(ds.RootField);

                    ds.MakeReadonly();

                    TestUtils.CollectionAssertEqual(
                        // ReSharper disable PossibleMultipleEnumeration
                        values, RoundTrip(ds, codec, values), comparer, "{0} {1}", typeof (T).Name, name);
                }
                catch (Exception x)
                {
                    string msg = string.Format(
                        "codec.Count={0}, codec.Buffer[pos-1]={1}",
                        codec.Count,
                        codec.Count > 0
                            ? codec.Buffer[codec.Count - 1].ToString(CultureInfo.InvariantCulture)
                            : "n/a");
                    if (x.GetType() == typeof (OverflowException))
                        throw new OverflowException(msg, x);

                    throw new SerializerException(x, msg);
                }
            }
        }

        private static IEnumerable<T> RoundTrip<T>(DynamicSerializer<T> ds, CodecWriter codec, IEnumerable<T> values)
        {
            using (IEnumerator<T> enmr = values.GetEnumerator())
            {
                bool moveNext = enmr.MoveNext();
                var buff = new Buffer<T>(new T[4]);

                while (moveNext)
                {
                    try
                    {
                        codec.Count = 0;
                        moveNext = ds.Serialize(codec, enmr);

                        codec.Count = 0;
                        buff.Count = 0;
                        using (var cdcRdr = new CodecReader(codec.AsArraySegment()))
                            ds.DeSerialize(cdcRdr, buff, int.MaxValue);
                    }
                    catch (Exception x)
                    {
                        string msg = string.Format(
                            "codec.Count={0}, codec.Buffer[pos-1]={1}, enmr.Value={2}",
                            codec.Count,
                            codec.Count > 0
                                ? codec.Buffer[codec.Count - 1].ToString(CultureInfo.InvariantCulture)
                                : "n/a",
                            moveNext ? enmr.Current.ToString() : "none left");

                        if (x.GetType() == typeof (OverflowException))
                            throw new OverflowException(msg, x);

                        throw new SerializerException(x, msg);
                    }

                    ArraySegment<T> result = buff.AsArraySegment();
                    for (int i = result.Offset; i < result.Count; i++)
                        yield return result.Array[i];
                }
            }
        }
    }
}