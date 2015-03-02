﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using Redwood.Framework.Parser;
using Redwood.VS2013Extension.RwHtmlEditorExtensions.Completions.RwHtml.Base;

namespace Redwood.VS2013Extension.RwHtmlEditorExtensions.Completions.RwHtml
{
    [Export(typeof(IRwHtmlCompletionProvider))]
    public class MainBindingNameCompletionProvider : BindingNameCompletionProviderBase
    {
        public override IEnumerable<SimpleRwHtmlCompletion> GetItems(RwHtmlCompletionContext context)
        {
            var bindingTypes = new[] { Constants.CommandBinding, Constants.ValueBinding, Constants.ResourceBinding };
            var userControlBindingTypes = new[] { Constants.ControlStateBinding, Constants.ControlCommandBinding, Constants.ControlPropertyBinding };

            return Enumerable.Concat(bindingTypes, userControlBindingTypes).Select(b => new SimpleRwHtmlCompletion(b, b + ": "));
        }
    }
}