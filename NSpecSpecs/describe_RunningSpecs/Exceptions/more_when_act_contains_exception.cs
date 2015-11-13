using NSpec;
using NSpec.Domain;
using NSpecSpecs.WhenRunningSpecs;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NSpecSpecs.describe_RunningSpecs.Exceptions
{
    [TestFixture]
    [Category("RunningSpecs")]
    public class more_when_act_contains_exception : when_running_specs
    {
        private class SpecClass : nspec
        {
            bool isTrue = false;

            void exception_thrown_in_act()
            {
                act = () =>
                {
                    MethodThrowsExceptionAndShouldBeInStackTrace();

                    isTrue = true;
                };

                it["is true"] = () => isTrue.should_be_true();
            }

            void MethodThrowsExceptionAndShouldBeInStackTrace()
            {
                throw new InvalidOperationException("Exception in act.");
            }
        }

        [SetUp]
        public void setup()
        {
            Run(typeof(SpecClass));
        }

        [Test]
        public void the_example_level_failure_should_indicate_a_context_failure()
        {
            TheExample("is true")
                .Exception.GetType().should_be(typeof(ExampleFailureException));
        }

        [Test]
        public void examples_with_only_act_failure_should_fail_because_of_act()
        {
            TheExample("is true").Exception
                .InnerException.GetType().should_be(typeof(InvalidOperationException));
        }
    }
}
