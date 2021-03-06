﻿using NUnit.Framework;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using LeanCloud.Storage;
using LeanCloud.Common;

namespace LeanCloud.Test {
    public class RelationTest {
        [SetUp]
        public void SetUp() {
            Logger.LogDelegate += Utils.Print;
            LeanCloud.Initialize("ikGGdRE2YcVOemAaRbgp1xGJ-gzGzoHsz", "NUKmuRbdAhg1vrb2wexYo1jo", "https://ikggdre2.lc-cn-n1-shared.com");
        }

        [TearDown]
        public void TearDown() {
            Logger.LogDelegate -= Utils.Print;
        }

        [Test]
        public async Task AddAndRemove() {
            LCObject parent = new LCObject("Parent");
            LCObject c1 = new LCObject("Child");
            parent.AddRelation("children", c1);
            LCObject c2 = new LCObject("Child");
            parent.AddRelation("children", c2);
            await parent.Save();

            LCRelation<LCObject> relation = parent["children"] as LCRelation<LCObject>;
            LCQuery<LCObject> query = relation.Query;
            int count = await query.Count();

            TestContext.WriteLine($"count: {count}");
            Assert.AreEqual(count, 2);

            parent.RemoveRelation("children", c2);
            await parent.Save();

            int count2 = await query.Count();
            TestContext.WriteLine($"count: {count2}");
            Assert.AreEqual(count2, 1);
        }

        [Test]
        public async Task Query() {
            LCQuery<LCObject> query = new LCQuery<LCObject>("Parent");
            LCObject parent = await query.Get("5e13112021b47e0070ed0922");
            LCRelation<LCObject> relation = parent["children"] as LCRelation<LCObject>;

            TestContext.WriteLine(relation.Key);
            TestContext.WriteLine(relation.Parent);
            TestContext.WriteLine(relation.TargetClass);

            Assert.NotNull(relation.Key);
            Assert.NotNull(relation.Parent);
            Assert.NotNull(relation.TargetClass);

            LCQuery<LCObject> relationQuery = relation.Query;
            List<LCObject> list = await relationQuery.Find();
            list.ForEach(item => {
                TestContext.WriteLine(item.ObjectId);
                Assert.NotNull(item.ObjectId);
            });
        }
    }
}
