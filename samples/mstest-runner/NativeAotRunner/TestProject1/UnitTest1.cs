using ClassLibrary1;

namespace TestProject1
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestMethod1()
        {
            Assert.AreEqual(3, new Class1().Add(1, 2));
        }

        [TestMethod]
        [DataRow(1, 2)]
        public void TestMethod2(int left, int right)
        {
            Assert.AreEqual(3, new Class1().Add(left, right));
        }

        [TestMethod]
        [DynamicData(nameof(Data))]
        public void TestMethod3(int left, int right)
        {
            Assert.AreEqual(3, new Class1().Add(left, right));
        }

        [TestMethod]
        [DynamicData(nameof(Users))]
        public void TestMethod4(User u)
        {
            Assert.AreEqual(3, new Class1().Add(u.Left, u.Right));
        }

        public static IEnumerable<object[]> Data { get; } =
        [
            [1, 2],
            [-3, 6],
        ];

        public static IEnumerable<object[]> Users { get; } =
        [
            [new User { Left = 1, Right = 2 }],
            [new User { Left = -3, Right = 6}],
        ];
    }

    public class User
    {
        public required int Left { get; init; }
        public required int Right { get; init; }
    }
}