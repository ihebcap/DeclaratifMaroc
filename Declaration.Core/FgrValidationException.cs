using System;
using SageTaxReader.Contracts;

namespace Declaration.Core
{
    public class FgrValidationException : Exception
    {
        public DocumentTaxesInfo Document { get; }

        public FgrValidationException(string message, DocumentTaxesInfo document) : base(message)
        {
            Document = document;
        }
    }
}
