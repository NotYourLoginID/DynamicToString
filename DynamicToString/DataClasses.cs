using System;

namespace DynamicToString
{
    public class MyBasicClass
    {
        public MyBasicClass(string s, int? i, bool b, double d)
        {
            MyString = s;
            MyNullableInt = i;
            MyBoolean = b;
            MyDouble = d;
            TotalInstances++;
        }

        public static int TotalInstances { get; private set; }
        public string MyString { get; set; }
        public int? MyNullableInt { get; set; }
        public bool MyBoolean { get; set; }
        public double MyDouble { get; set; }
    }

    public class MyStringableClass
    {
        public MyStringableClass(string s, int? i, bool b, double d)
        {
            MyString = s;
            MyNullableInt = i;
            MyBoolean = b;
            MyDouble = d;
            TotalInstances++;
        }

        public static int TotalInstances { get; private set; }
        public string MyString { get; set; }
        public int? MyNullableInt { get; set; }
        public bool MyBoolean { get; set; }
        public double MyDouble { get; set; }

        public override string ToString()
        {
            return $"OVERRIDE [{nameof(MyString)}: {MyString}]";
        }
    }

    public class MyExtendedClass : MyStringableClass
    {
        public MyExtendedClass(string s, int? i, bool b, double d) : base(s, i, b, d)
        {
        }
    }

    public class MySuperExtendedClass : MyStringableClass
    {
        public MySuperExtendedClass(string s, int? i, bool b, double d) : base(s, i, b, d)
        {
            MyCreationDate = DateTime.Now;
        }

        public DateTime MyCreationDate { get; }

        public override string ToString()
        {
            return $"THIS IS A SUPER OVERRIDE [{nameof(MyString)}: {MyString}]";
        }
    }

    public class Shape
    {
        public Shape(string name)
        {
            Name = name;
        }

        public string Name { get; set; }

        public override string ToString()
        {
            return $"ShapeName: {Name}";
        }
    }

    public class Circle : Shape
    {
        public Circle(string name) : base(name)
        {
        }

        public override string ToString()
        {
            return $"CircleName: {Name}";
        }
    }

    public class Square : Shape
    {
        public Square(string name) : base(name)
        {
        }

        public int Sides { get; } = 4;
    }
}