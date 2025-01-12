﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NSpec.Domain.Formatters;
using System.Threading.Tasks;

namespace NSpec.Domain
{
    public class Context
    {
        public void RunBefores(nspec instance)
        {
            // parent chain

            RecurseAncestors(c => c.RunBefores(instance));

            // class (method-level)

            if (BeforeInstance != null && BeforeInstanceAsync != null)
            {
                throw new ArgumentException("A single class cannot have both a sync and an async class-level 'before_each' set, please pick one of the two");
            }

            BeforeInstance.SafeInvoke(instance);

            BeforeInstanceAsync.SafeInvoke(instance);

            // context-level

            if (Before != null && BeforeAsync != null)
            {
                throw new ArgumentException("A single context cannot have both a 'before' and an 'beforeAsync' set, please pick one of the two");
            }

            Before.SafeInvoke();

            BeforeAsync.SafeInvoke();
        }

        void RunBeforeAll(nspec instance)
        {
            // context-level

            if (BeforeAll != null && BeforeAllAsync != null)
            {
                throw new ArgumentException("A single context cannot have both a 'beforeAll' and an 'beforeAllAsync' set, please pick one of the two");
            }

            BeforeAll.SafeInvoke();

            BeforeAllAsync.SafeInvoke();

            // class (method-level)

            if (BeforeAllInstance != null && BeforeAllInstanceAsync != null)
            {
                throw new ArgumentException("A single class cannot have both a sync and an async class-level 'before_all' set, please pick one of the two");
            }

            BeforeAllInstance.SafeInvoke(instance);

            BeforeAllInstanceAsync.SafeInvoke(instance);
        }

        public void RunActs(nspec instance)
        {
            // parent chain

            RecurseAncestors(c => c.RunActs(instance));

            // class (method-level)

            if (ActInstance != null && ActInstanceAsync != null)
            {
                throw new ArgumentException("A single class cannot have both a sync and an async class-level 'act_each' set, please pick one of the two");
            }

            ActInstance.SafeInvoke(instance);

            ActInstanceAsync.SafeInvoke(instance);

            // context-level

            if (Act != null && ActAsync != null)
            {
                throw new ArgumentException("A single context cannot have both an 'act' and an 'actAsync' set, please pick one of the two");
            }

            Act.SafeInvoke();

            ActAsync.SafeInvoke();
        }

        public void RunAfters(nspec instance)
        {
            // context-level

            if (After != null && AfterAsync != null)
            {
                throw new ArgumentException("A single context cannot have both an 'after' and an 'afterAsync' set, please pick one of the two");
            }

            After.SafeInvoke();

            AfterAsync.SafeInvoke();

            // class (method-level)

            if (AfterInstance != null && AfterInstanceAsync != null)
            {
                throw new ArgumentException("A single class cannot have both a sync and an async class-level 'after_each' set, please pick one of the two");
            }

            AfterInstance.SafeInvoke(instance);

            AfterInstanceAsync.SafeInvoke(instance);

            // parent chain

            RecurseAncestors(c => c.RunAfters(instance));
        }

        public void RunAfterAll(nspec instance)
        {
            // context-level

            if (AfterAll != null && AfterAllAsync != null)
            {
                throw new ArgumentException("A single context cannot have both an 'afterAll' and an 'afterAllAsync' set, please pick one of the two");
            }

            AfterAll.SafeInvoke();

            AfterAllAsync.SafeInvoke();

            // class (method-level)

            if (AfterAllInstance != null && AfterAllInstanceAsync != null)
            {
                throw new ArgumentException("A single class cannot have both a sync and an async class-level 'after_all' set, please pick one of the two");
            }

            AfterAllInstance.SafeInvoke(instance);

            AfterAllInstanceAsync.SafeInvoke(instance);
        }

        public void AddExample(ExampleBase example)
        {
            example.Context = this;

            example.Tags.AddRange(Tags);

            Examples.Add(example);

            example.Pending |= IsPending();
        }

        public IEnumerable<ExampleBase> AllExamples()
        {
            return Contexts.Examples().Union(Examples);
        }

        public bool IsPending()
        {
            return isPending || (Parent != null && Parent.IsPending());
        }

        public IEnumerable<ExampleBase> Failures()
        {
            return AllExamples().Where(e => e.Exception != null);
        }

        public void AddContext(Context child)
        {
            child.Level = Level + 1;

            child.Parent = this;

            child.Tags.AddRange(child.Parent.Tags);

            Contexts.Add(child);
        }

        /// <summary>
        /// Test execution happens in two phases: this is the first phase.
        /// </summary>
        /// <remarks>
        /// Here all contexts and all their examples are run, collecting distinct exceptions 
        /// from context itself (befores/ acts/ it/ afters), beforeAll, afterAll.
        /// </remarks>
        public virtual void Run(ILiveFormatter formatter, bool failFast, nspec instance = null)
        {
            if (failFast && Parent.HasAnyFailures()) return;

            var nspec = savedInstance ?? instance;

            bool runBeforeAfterAll = AnyUnfilteredExampleInSubTree(nspec);

            if (runBeforeAfterAll) RunAndHandleException(RunBeforeAll, nspec, ref ExceptionBeforeAll);

            // intentionally using for loop to prevent collection was modified error in sample specs
            for (int i = 0; i < Examples.Count; i++)
            {
                var example = Examples[i];

                if (failFast && example.Context.HasAnyFailures()) return;

                Exercise(example, nspec);

                if (example.HasRun && !alreadyWritten)
                {
                    WriteAncestors(formatter);
                    alreadyWritten = true;
                }

                if (example.HasRun) formatter.Write(example, Level);
            }

            Contexts.Do(c => c.Run(formatter, failFast, nspec));

            if (runBeforeAfterAll) RunAndHandleException(RunAfterAll, nspec, ref ExceptionAfterAll);
        }

        /// <summary>
        /// Test execution happens in two phases: this is the second phase.
        /// </summary>
        /// <remarks>
        /// Here all contexts and all their examples are traversed again to set proper exception
        /// on examples, giving priority to exceptions from: inherithed beforeAll, beforeAll,
        /// context (befores/ acts/ it/ afters), afterAll, inherithed afterAll.
        /// </remarks>
        public virtual void AssignExceptions()
        {
            AssignExceptions(null, null);
        }

        protected virtual void AssignExceptions(Exception inheritedBeforeAllException, Exception inheritedAfterAllException)
        {
            inheritedBeforeAllException = inheritedBeforeAllException ?? ExceptionBeforeAll;
            inheritedAfterAllException = ExceptionAfterAll ?? inheritedAfterAllException;

            // if thrown exception was correctly expected, ignore this context Exception
            Exception unexpectedException = ClearExpectedException ? null : Exception;

            Exception contextException = (inheritedBeforeAllException ?? unexpectedException) ?? inheritedAfterAllException;

            for (int i = 0; i < Examples.Count; i++)
            {
                var example = Examples[i];

                if (!example.Pending)
                {
                    example.AssignProperException(contextException);
                }
            }

            Contexts.Do(c => c.AssignExceptions(inheritedBeforeAllException, inheritedAfterAllException));
        }

        public virtual void Build(nspec instance = null)
        {
            instance.Context = this;

            savedInstance = instance;

            Contexts.Do(c => c.Build(instance));
        }

        public string FullContext()
        {
            return Parent != null ? Parent.FullContext() + ". " + Name : Name;
        }

        public bool RunAndHandleException(Action<nspec> action, nspec nspec, ref Exception exceptionToSet)
        {
            bool hasThrown = false;

            try
            {
                action(nspec);
            }
            catch (TargetInvocationException invocationException)
            {
                if (exceptionToSet == null) exceptionToSet = nspec.ExceptionToReturn(invocationException.InnerException);

                hasThrown = true;
            }
            catch (Exception exception)
            {
                if (exceptionToSet == null) exceptionToSet = nspec.ExceptionToReturn(exception);

                hasThrown = true;
            }

            return hasThrown;
        }

        public void Exercise(ExampleBase example, nspec nspec)
        {
            if (example.ShouldSkip(nspec.tagsFilter)) return;

            RunAndHandleException(RunBefores, nspec, ref Exception);

            RunAndHandleException(RunActs, nspec, ref Exception);

            RunAndHandleException(example.Run, nspec, ref example.Exception);

            bool exceptionThrownInAfters = RunAndHandleException(RunAfters, nspec, ref Exception);

            // when an expected exception is thrown and is set to be cleared by 'expect<>',
            // a subsequent exception thrown in 'after' hooks would go unnoticed, so do not clear in this case

            if (exceptionThrownInAfters) ClearExpectedException = false;
        }

        public virtual bool IsSub(Type baseType)
        {
            return false;
        }

        public nspec GetInstance()
        {
            return savedInstance ?? Parent.GetInstance();
        }

        public IEnumerable<Context> AllContexts()
        {
            return new[] { this }.Union(ChildContexts());
        }

        public IEnumerable<Context> ChildContexts()
        {
            return Contexts.SelectMany(c => new[] { c }.Union(c.ChildContexts()));
        }

        public bool HasAnyFailures()
        {
            return AllExamples().Any(e => e.Failed());
        }

        public bool HasAnyExecutedExample()
        {
            return AllExamples().Any(e => e.HasRun);
        }

        public void TrimSkippedDescendants()
        {
            Contexts.RemoveAll(c => !c.HasAnyExecutedExample());

            Examples.RemoveAll(e => !e.HasRun);

            Contexts.Do(c => c.TrimSkippedDescendants());
        }

        bool AnyUnfilteredExampleInSubTree(nspec nspec)
        {
            Func<ExampleBase, bool> shouldNotSkip = e => e.ShouldNotSkip(nspec.tagsFilter);

            bool anyExampleOrSubExample = Examples.Any(shouldNotSkip) || Contexts.Examples().Any(shouldNotSkip);

            return anyExampleOrSubExample;
        }

        public override string ToString()
        {
            string exceptionText = (Exception != null ? ", " + Exception.GetType().Name : String.Empty);

            return String.Format("{0}, L{1}, {2} exm, {3} ctx{4}", Name, Level, Examples.Count, Contexts.Count, exceptionText);
        }

        void RecurseAncestors(Action<Context> ancestorAction)
        {
            if (Parent != null) ancestorAction(Parent);
        }

        void WriteAncestors(ILiveFormatter formatter)
        {
            if (Parent == null) return;

            Parent.WriteAncestors(formatter);

            if (!alreadyWritten) formatter.Write(this);

            alreadyWritten = true;
        }

        public Context(string name = "", string tags = null, bool isPending = false)
        {
            Name = name.Replace("_", " ");
            Examples = new List<ExampleBase>();
            Contexts = new ContextCollection();
            Tags = Domain.Tags.ParseTags(tags);
            this.isPending = isPending;
        }

        public string Name;
        public int Level;
        public List<string> Tags;
        public List<ExampleBase> Examples;
        public ContextCollection Contexts;
        public Action Before, Act, After, BeforeAll, AfterAll;
        public Action<nspec> BeforeInstance, ActInstance, AfterInstance, BeforeAllInstance, AfterAllInstance;
        public Func<Task> BeforeAsync, ActAsync, AfterAsync, BeforeAllAsync, AfterAllAsync;
        public Action<nspec> BeforeInstanceAsync, ActInstanceAsync, AfterInstanceAsync, BeforeAllInstanceAsync, AfterAllInstanceAsync;
        public Context Parent;
        public Exception ExceptionBeforeAll, Exception, ExceptionAfterAll;
        public bool ClearExpectedException;

        nspec savedInstance;
        bool alreadyWritten, isPending;
    }
}