using System;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.SharedModels.Exceptions;

public class ResolveEntityReferenceException : Exception
{
    public ResolveEntityReferenceException() : base("Multiple ResultingEntity Hub entities were found for the same source system entity")
    {
    }

    public ResolveEntityReferenceException(Exception innerException = null) : base("Multiple ResultingEntity Hub entities were found for the same source system entity", innerException)
    {
    }

    public ResolveEntityReferenceException(ExternalEntityReference unresolvedEntityReference = null, Exception innerException = null) : this(innerException)
    {
        UnresolvedEntityReference = unresolvedEntityReference;
    }

    public ExternalEntityReference UnresolvedEntityReference { get; set; }
}