using System;
using System.Collections.Generic;
using System.Linq;
using DotVVM.Framework.Controls;
using DotVVM.Framework.Hosting;
using Newtonsoft.Json;

namespace DotVVM.Framework.ResourceManagement
{
    /// <summary>
    /// Piece of javascript code that is used in the page.
    /// </summary>
    public class InlineScriptResource : ResourceBase
    {
        [Obsolete("Code parameter is required, please provide it in the constructor.")]
        public InlineScriptResource(ResourceRenderPosition renderPosition = ResourceRenderPosition.Body) : base(renderPosition)
        {
        }

        [JsonConstructor]
        public InlineScriptResource(string code, ResourceRenderPosition renderPosition = ResourceRenderPosition.Body) : base(renderPosition)
        {
            this.Code = code;
        }

        /// <summary>
        /// Gets or sets the javascript code that will be embedded in the page.
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// Renders the resource in the specified <see cref="IHtmlWriter" />.
        /// </summary>
        public override void Render(IHtmlWriter writer, IDotvvmRequestContext context, string resourceName)
        {
            if (string.IsNullOrWhiteSpace(Code)) return;
            writer.AddAttribute("type", "text/javascript");
            writer.RenderBeginTag("script");
            writer.WriteUnencodedText(Code);
            writer.RenderEndTag();
        }
    }
}