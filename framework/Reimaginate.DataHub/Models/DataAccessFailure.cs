using System;
using Reimaginate.DataServices.Responses;

namespace Reimaginate.DataHub.Models;

public class DataAccessFailure<T>(T item, Exception ex) : BulkOperationFailure<T>(item, ex);