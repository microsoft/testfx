// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Data;

internal partial class TestDataConnectionSql
{
    #region Quotes

    [MemberNotNull(nameof(_quotePrefix))]
#pragma warning disable SA1201 // Elements must appear in the correct order
    public virtual string QuotePrefix
#pragma warning restore SA1201 // Elements must appear in the correct order
    {
        get
        {
            if (StringEx.IsNullOrEmpty(_quotePrefix))
            {
                GetQuoteLiterals();
            }

            return _quotePrefix;
        }

        set => _quotePrefix = value;
    }

    [MemberNotNull(nameof(_quoteSuffix))]
    public virtual string QuoteSuffix
    {
        get
        {
            if (StringEx.IsNullOrEmpty(_quoteSuffix))
            {
                GetQuoteLiterals();
            }

            return _quoteSuffix;
        }

        set => _quoteSuffix = value;
    }

    private char CatalogSeparatorChar
    {
        get
        {
            if (CommandBuilder != null)
            {
                string catalogSeparator = CommandBuilder.CatalogSeparator;
                if (!StringEx.IsNullOrEmpty(catalogSeparator))
                {
                    DebugEx.Assert(catalogSeparator.Length == 1, "catalogSeparator should have 1 element.");
                    return catalogSeparator[0];
                }
            }

            return '.';
        }
    }

    private char SchemaSeparatorChar
    {
        get
        {
            if (CommandBuilder != null)
            {
                string schemaSeparator = CommandBuilder.SchemaSeparator;
                if (!StringEx.IsNullOrEmpty(schemaSeparator))
                {
                    DebugEx.Assert(schemaSeparator.Length == 1, "schemaSeparator should have 1 element.");
                    return schemaSeparator[0];
                }
            }

            return '.';
        }
    }

    /// <summary>
    /// Note that for Oledb and Odbc CommandBuilder.QuotePrefix/Suffix is empty.
    /// So we use GetQuoteLiterals for those. For all others we use CommandBuilder.QuotePrefix/Suffix.
    /// </summary>
    [MemberNotNull(nameof(_quotePrefix), nameof(_quoteSuffix))]
    public virtual void GetQuoteLiterals()
    {
        _quotePrefix = CommandBuilder.QuotePrefix;
        _quoteSuffix = CommandBuilder.QuoteSuffix;
    }

    protected virtual string QuoteIdentifier(string identifier)
    {
        DebugEx.Assert(!StringEx.IsNullOrEmpty(identifier), "identifier should not be null.");
        return CommandBuilder.QuoteIdentifier(identifier);
    }

    protected virtual string UnquoteIdentifier(string identifier)
    {
        DebugEx.Assert(!StringEx.IsNullOrEmpty(identifier), "identifier should not be null.");
        return CommandBuilder.UnquoteIdentifier(identifier);
    }

    [MemberNotNull(nameof(_quotePrefix), nameof(_quoteSuffix), nameof(QuotePrefix), nameof(QuoteSuffix))]
    protected void GetQuoteLiteralsHelper()
    {
        // Try to get quote chars by hand for those providers that for some reason return empty QuotePrefix/Suffix.
        string s = "abcdefgh";
        string quoted = QuoteIdentifier(s);
        string[] parts = quoted.Split([s], StringSplitOptions.None);

        DebugEx.Assert(parts is { Length: 2 }, "TestDataConnectionSql.GetQuotesLiteralHelper: Failure when trying to quote an identifier!");
        DebugEx.Assert(!StringEx.IsNullOrEmpty(parts[0]), "TestDataConnectionSql.GetQuotesLiteralHelper: Trying to set empty value for QuotePrefix!");
        DebugEx.Assert(!StringEx.IsNullOrEmpty(parts[1]), "TestDataConnectionSql.GetQuotesLiteralHelper: Trying to set empty value for QuoteSuffix!");

        QuotePrefix = parts[0];
        QuoteSuffix = parts[1];
    }

    #endregion
}

#endif
