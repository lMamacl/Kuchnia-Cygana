using System;
using System.Collections.Generic;
using FluentAssertions;
using FluentValidation.TestHelper;
using KuchniaUCygana.Application.DTOs.Packing;
using KuchniaUCygana.Application.DTOs.Warehouse;
using KuchniaUCygana.Application.Validators;
using Xunit;

namespace KuchniaUCygana.Tests.Unit.Warehouse;

public class ValidatorsTests
{
    private readonly ManualIssueValidator _manualIssueValidator;
    private readonly EditBatchExpiryValidator _editBatchExpiryValidator;
    private readonly BulkPrintValidator _bulkPrintValidator;

    public ValidatorsTests()
    {
        _manualIssueValidator = new ManualIssueValidator();
        _editBatchExpiryValidator = new EditBatchExpiryValidator();
        _bulkPrintValidator = new BulkPrintValidator();
    }

    [Fact]
    public void ManualIssueValidator_ShouldHaveError_WhenFieldsAreInvalid()
    {
        // Arrange
        var request = new ManualIssueRequest
        {
            StockItemId = 0,
            Quantity = 0,
            Reason = string.Empty,
            IssuedTo = string.Empty
        };

        // Act
        var result = _manualIssueValidator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.StockItemId);
        result.ShouldHaveValidationErrorFor(x => x.Quantity);
        result.ShouldHaveValidationErrorFor(x => x.Reason);
        result.ShouldHaveValidationErrorFor(x => x.IssuedTo);
    }

    [Fact]
    public void ManualIssueValidator_ShouldBeValid_WhenRequestIsValid()
    {
        // Arrange
        var request = new ManualIssueRequest
        {
            StockItemId = 1,
            Quantity = 10.5m,
            Reason = "Pobranie do kuchni",
            IssuedTo = "Jan Kowalski"
        };

        // Act
        var result = _manualIssueValidator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EditBatchExpiryValidator_ShouldHaveError_WhenFieldsAreInvalid()
    {
        // Arrange
        var request = new EditBatchExpiryRequest
        {
            BatchId = 0,
            NewExpiryDate = DateTimeOffset.UtcNow.AddDays(-1), // przeszłość
            Reason = "Krót" // mniej niż 5 znaków
        };

        // Act
        var result = _editBatchExpiryValidator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.BatchId);
        result.ShouldHaveValidationErrorFor(x => x.NewExpiryDate);
        result.ShouldHaveValidationErrorFor(x => x.Reason);
    }

    [Fact]
    public void EditBatchExpiryValidator_ShouldBeValid_WhenRequestIsValid()
    {
        // Arrange
        var request = new EditBatchExpiryRequest
        {
            BatchId = 1,
            NewExpiryDate = DateTimeOffset.UtcNow.AddDays(5),
            Reason = "Prawidłowy powód zmiany daty ważności"
        };

        // Act
        var result = _editBatchExpiryValidator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void BulkPrintValidator_ShouldHaveError_WhenRequestIsInvalid()
    {
        // Arrange
        var request = new BulkPrintRequest
        {
            SessionIds = new List<int>(), // pusta lista
            OperatorName = string.Empty
        };

        // Act
        var result = _bulkPrintValidator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.SessionIds);
        result.ShouldHaveValidationErrorFor(x => x.OperatorName);
    }

    [Fact]
    public void BulkPrintValidator_ShouldHaveError_WhenSessionIdsExceedMax()
    {
        // Arrange
        var sessions = new List<int>();
        for (int i = 0; i < 51; i++)
        {
            sessions.Add(i + 1);
        }

        var request = new BulkPrintRequest
        {
            SessionIds = sessions,
            OperatorName = "Operator"
        };

        // Act
        var result = _bulkPrintValidator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.SessionIds);
    }

    [Fact]
    public void BulkPrintValidator_ShouldBeValid_WhenRequestIsValid()
    {
        // Arrange
        var request = new BulkPrintRequest
        {
            SessionIds = new List<int> { 1, 2, 3 },
            OperatorName = "Jan Operator"
        };

        // Act
        var result = _bulkPrintValidator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
