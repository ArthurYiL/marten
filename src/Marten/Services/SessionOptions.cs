﻿using System.Collections.Generic;
using System.Data;
using Marten.Storage;

namespace Marten.Services
{
    public sealed class SessionOptions
    {
        /// <summary>
        /// Default to DocumentTracking.IdentityOnly
        /// </summary>
        public DocumentTracking Tracking { get; set; } = DocumentTracking.IdentityOnly;

        /// <summary>
        /// Default to 30 seconds
        /// </summary>
        public int Timeout { get; set; } = 30;

        /// <summary>
        /// Default to IsolationLevel.ReadCommitted
        /// </summary>
        public IsolationLevel IsolationLevel { get; set; } = IsolationLevel.ReadCommitted;

        /// <summary>
        ///     Add, remove, or reorder local session listeners
        /// </summary>
        public readonly IList<IDocumentSessionListener> Listeners = new List<IDocumentSessionListener>();

        /// <summary>
        /// Override the tenant id for the requested session
        /// </summary>
        public string TenantId { get; set; } = Tenancy.DefaultTenantId;
    }
}