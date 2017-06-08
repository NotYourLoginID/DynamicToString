using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DynamicToString
{
    class Program
    {
        private static Random rand;

        static Program()
        {
            
        }

        static void Main(string[] args)
        {
            rand = new Random();
            var basic1 = new MyBasicClass("Mr. Basic", null, true, 1.0);
            var basic2 = new MyBasicClass("Mrs. Basic", 5, false, 999);
            var basicList = new[] { basic1, basic2, basic1, null, basic2 }.ToList();
            var shape1 = new Shape("Mr. Shape");
            var shape2 = new Shape("Mrs. Shape");
            var shapeList = new[] { shape1, shape2, null, shape1 }.ToList();
            var circle1 = new Circle("Mr. Circle");
            var square1 = new Square("Mr. Square");

            //var basicList = RandomMyBasicClass(20);
            //var basicList2 = RandomMyBasicClass(200);

            //var x = RandomMyBasicClass();

            //var nullConst = Expression.Constant("<null>");
            //var instance = Expression.Parameter(typeof(object), "obj");
            ////var result = Expression.Parameter(typeof(string));

            //var toStringMethodInfo = typeof(object).GetMethod("ToString", BindingFlags.Instance | BindingFlags.Public);
            //var callExpr = Expression.Call(instance, typeof(object).GetMethod("ToString", BindingFlags.Instance|BindingFlags.Public));

            //var lamExpr = Expression.Lambda<Func<object, string>>(callExpr, instance);

            //var exp = Expression.Equal(instance, Expression.Constant(null, typeof(object)));
            //Expression<Func<string, string>> symbolAdderExpression = s => "<" + s + ">";

            //var exp2 = Expression.IfThenElse(Expression.ReferenceEqual(instance, Expression.Constant(null)), Expression.Constant("<null>"), Expression.Lambda<Func<object, string>>(Expression.Call(instance, toStringMethodInfo), instance));

            //var lam2 = Expression.Lambda<Func<object, string>>(exp2, instance).Body;

            //var result = lam(shape1);
            //var result2 = lam2(shape1);

            //InternalExtensions.SetAutoStringMethod<MyBasicClass>(x => $"{nameof(MyBasicClass)} Object - {x.MyString}");

            var bigList = RandomMyBasicClass(200).ToList();

            var output = bigList.TimeListAutoToString();
            Console.Out.WriteLine(output);

            bigList = RandomMyBasicClass(200).ToList();
            output = bigList.TimeListAutoToString();
            Console.Out.WriteLine(output);


            //output = basicList2.TimeAutoToString();
            //Console.Out.WriteLine(output);

        }

        public static MyBasicClass RandomMyBasicClass()
        {
            string s;
            switch (rand.Next(0, 4))
            {
                case 0:
                    s = "Chris";
                    break;
                case 1:
                    s = "Bob";
                    break;
                case 2:
                    s = "Alice";
                    break;
                case 3:
                    s = "Randy";
                    break;
                default:
                    s = "Lemons";
                    break;
            }

            int? i = rand.Next(0, 3);
            i = i <= 1 ?  (int?) null : i;

            var b = i == null;

            var d = rand.NextDouble();
            return new MyBasicClass(s, i, b, d);
        }

        public static IEnumerable<MyBasicClass> RandomMyBasicClass(int count)
        {
            count = count < 1 ? 1 : count;
            return Enumerable.Range(0, count).Select(x => RandomMyBasicClass());
        }
    }
}
