using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RideMatchProject.LoginClasses
{
    public class ValidationResult
    {
        public bool IsValid { get; }
        public string ErrorMessage { get; }

        public ValidationResult(bool isValid, string errorMessage)
        {
            IsValid = isValid;
            ErrorMessage = errorMessage;
        }
    }
}
