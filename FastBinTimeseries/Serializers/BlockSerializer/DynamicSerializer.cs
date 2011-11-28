using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using JetBrains.Annotations;

namespace NYurik.FastBinTimeseries.Serializers.BlockSerializer
{
    internal static class DynamicSerializer<T>
    {
        ///
        /// Generated code:
        /// 
        /// 
        /// * codec - writes output to this codec using Write*() methods
        /// * enumerator to go through the input T values.
        ///     MoveNext() had to be called on it before passing it in.
        /// * returns false if no more items, or true if there are more items but the codec buffer is full
        /// 
        /// bool Serialize(StreamCodec codec, IEnumerator&ltT> enumerator)
        /// {
        ///     bool moveNext;
        ///     int count = 1;
        ///     int codecPos;
        ///     T current = enumerator.Current;
        ///     
        ///     var state1, state2, ...;
        /// 
        ///     codec.Write(state1);
        ///     codec.Write(state2);
        ///     ...
        /// 
        ///     while (true) {
        /// 
        ///         moveNext = enumerator.MoveNext();
        ///         if (!moveNext)
        ///             break;
        /// 
        ///         codecPos = codec.BufferPos;
        ///         if (! (codec.Write(delta1) && codec.Write(delta2) && ...) ) {
        ///             codec.BufferPos = codecPos;
        ///             break;
        ///         }
        /// 
        ///         count++;
        ///     }
        /// 
        ///     codec.WriteHeader(count);
        /// 
        ///     return moveNext;
        /// }
        public static Func<StreamCodec, IEnumerator<T>, bool> GenerateSerializer([NotNull] BaseSerializer serializer)
        {
            if (serializer == null)
                throw new ArgumentNullException("serializer");

            // param: codec
            ParameterExpression codecParam = Expression.Parameter(typeof (StreamCodec), "codec");

            // param: IEnumerator<T> data
            ParameterExpression enumeratorParam = Expression.Parameter(typeof (IEnumerator<T>), "enumerator");

            // bool moveNext;
            ParameterExpression moveNextVar = Expression.Variable(typeof (bool), "moveNext");

            // int count;
            ParameterExpression countVar = Expression.Variable(typeof (int), "count");

            // T current;
            ParameterExpression currentVar = Expression.Variable(typeof (T), "current");

            // current = enumerator.Current;
            BinaryExpression setCurrentExp = Expression.Assign(
                currentVar, Expression.PropertyOrField(enumeratorParam, "Current"));

            LabelTarget breakLabel = Expression.Label("loopBreak");

            var stateVariables = new List<ParameterExpression> {currentVar, countVar, moveNextVar};
            var writeInitStates = new List<Expression>();
            var methodBody = new List<Expression>();

            Expression writeDeltasExp = serializer.GetSerializer(
                currentVar, codecParam, stateVariables, writeInitStates);

            // count = 1;
            methodBody.Add(Expression.Assign(countVar, Expression.Constant(1)));

            // current = enumerator.Current;
            methodBody.Add(setCurrentExp);

            // codec.SkipHeader();
            methodBody.Add(Expression.Call(codecParam, "SkipHeader", null));

            // var state1 = current.Field1; codec.Write(state1); ...
            methodBody.AddRange(writeInitStates);


            // int codecPos;
            ParameterExpression codecPosExp = Expression.Parameter(typeof (int), "codecPos");

            // codec.BufferPos
            MemberExpression codecBufferPos = Expression.PropertyOrField(codecParam, "BufferPos");

            // while (true)
            methodBody.Add(
                Expression.Loop(
                    Expression.Block(
                        new Expression[]
                            {
                                // moveNext = enumerator.MoveNext();
                                Expression.Assign(
                                    moveNextVar,
                                    Expression.Call(enumeratorParam, typeof (IEnumerator).GetMethod("MoveNext"))),
                                // if
                                Expression.IfThen(
                                    // (!moveNext)
                                    Expression.IsFalse(moveNextVar),
                                    // break;
                                    Expression.Break(breakLabel)),
                                // current = enumerator.Current;
                                setCurrentExp,
                                Expression.Block(
                                    // int codecPos;
                                    new[] {codecPosExp},
                                    // codecPos = codec.BufferPos
                                    Expression.Assign(codecPosExp, codecBufferPos),
                                    // if (!writeDeltas)
                                    Expression.IfThen(
                                        Expression.Not(writeDeltasExp),
                                        Expression.Block(
                                            // codec.BufferPos = codecPos;
                                            Expression.Assign(codecBufferPos, codecPosExp),
                                            // break;
                                            Expression.Break(breakLabel)
                                            )
                                        )),
                                // count++;
                                Expression.PreIncrementAssign(countVar)
                            }),
                    breakLabel));

            methodBody.Add(Expression.Call(codecParam, "WriteHeader", null, countVar));

            // return moveNext;
            methodBody.Add(moveNextVar);

            Expression<Func<StreamCodec, IEnumerator<T>, bool>> serializeExp =
                Expression.Lambda<Func<StreamCodec, IEnumerator<T>, bool>>(
                    Expression.Block(stateVariables, methodBody),
                    "Serialize",
                    // parameter codec, IEnumerator<T>
                    new[] {codecParam, enumeratorParam}
                    );

            return serializeExp.Compile();
        }

        ///
        /// Generated code:
        /// 
        /// * codec to read the values from
        /// * result will get all the generated values
        /// * maxItemCount - maximum number of items to be deserialized
        /// 
        /// void DeSerialize(StreamCodec codec, Buff&lt;T> result, int maxItemCount)
        /// {
        ///     int count = codec.ReadHeader();
        ///     if (count > maxItemCount)
        ///         count = maxItemCount;
        ///     
        ///     var state1, state2, ...;
        /// 
        ///     result.Add(ReadField(codec, ref state));
        /// 
        ///     while (true) {
        /// 
        ///         count--;
        ///         if (count == 0)
        ///             break;
        /// 
        ///         result.Add(ReadField(codec, ref state));
        ///     }
        /// }
        /// 
        public static Action<StreamCodec, Buff<T>, int> GenerateDeSerializer([NotNull] BaseSerializer serializer)
        {
            if (serializer == null) throw new ArgumentNullException("serializer");

            // param: codec
            ParameterExpression codecParam = Expression.Parameter(typeof (StreamCodec), "codec");

            // param: Buff<T> result
            ParameterExpression resultParam = Expression.Parameter(typeof (Buff<T>), "result");

            // param: int maxItemCount
            ParameterExpression maxItemCountParam = Expression.Parameter(typeof (int), "maxItemCount");

            // int count;
            ParameterExpression countVar = Expression.Variable(typeof (int), "count");

            LabelTarget breakLabel = Expression.Label("loopBreak");

            var stateVariables = new List<ParameterExpression> {countVar};

            Expression readInitValue, readNextValue;
            serializer.GetDeSerializer(codecParam, stateVariables, out readInitValue, out readNextValue);

            var methodBody =
                new List<Expression>
                    {
                        // count = codec.ReadHeader();
                        Expression.Assign(countVar, Expression.Call(codecParam, "ReadHeader", null)),
                        // if (maxItemCount < count)
                        //     count = maxItemCount;
                        Expression.IfThen(
                            Expression.LessThan(maxItemCountParam, countVar),
                            Expression.Assign(countVar, maxItemCountParam)),
                        // result.Add(ReadField(codec, ref state));
                        Expression.Call(resultParam, "Add", null, readInitValue)
                    };

            var loopBody =
                new List<Expression>
                    {
                        // count--;
                        Expression.PreDecrementAssign(countVar),
                        // if (
                        Expression.IfThen(
                            // count == 0)
                            Expression.Equal(countVar, Expression.Constant(0)),
                            // break;
                            Expression.Break(breakLabel)),
                        // result.Add(ReadField(codec, ref state));
                        Expression.Call(resultParam, "Add", null, readNextValue)
                    };

            // while (true) { loopBody; }
            methodBody.Add(
                Expression.Loop(
                    Expression.Block(loopBody),
                    breakLabel));

            Expression<Action<StreamCodec, Buff<T>, int>> deSerializeExp =
                Expression.Lambda<Action<StreamCodec, Buff<T>, int>>(
                    Expression.Block(stateVariables, methodBody),
                    "DeSerialize",
                    // parameter StreamCodec codec, Buff&lt;T> result, int maxItemCount
                    new[] {codecParam, resultParam, maxItemCountParam}
                    );

            return deSerializeExp.Compile();
        }
    }

    internal class Buff<T>
    {
        private T[] _buffer;
        private int _count;

        public Buff()
        {
            _buffer = new T[4];
        }

        public ArraySegment<T> Buffer
        {
            get { return new ArraySegment<T>(_buffer, 0, _count); }
        }

        public void Add(T value)
        {
            if (_count == _buffer.Length)
            {
                var tmp = new T[_buffer.Length*2];
                Array.Copy(_buffer, tmp, _buffer.Length);
                _buffer = tmp;
            }
            _buffer[_count++] = value;
        }

        public void Reset()
        {
            Array.Clear(_buffer, 0, _count);
            _count = 0;
        }
    }
}