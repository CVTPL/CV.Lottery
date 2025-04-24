// admin-modal-register.js
// Handles AJAX form submission for the Register Admin modal
// - Client-side validation first
// - AJAX submit if valid
// - Modal stays open and updates with validation errors
// - Modal closes and reloads page on success

$(document).on('submit', '#addAdminModal form', function (e) {
    var $form = $(this);
    if (!$form.valid()) {
        // Don't submit if client-side validation fails
        e.preventDefault();
        e.stopImmediatePropagation();
        return false;
    }
    e.preventDefault();
    var formData = $form.serialize();
    var actionUrl = $form.attr('action') || window.location.pathname;
    $.ajax({
        url: actionUrl,
        type: 'POST',
        data: formData,
        headers: { 'X-Requested-With': 'XMLHttpRequest' },
        success: function (html) {
            // Try to extract modal body from returned HTML
            var $modalBody = $(html).find('#addAdminModal .modal-body');
            if ($modalBody.length) {
                $('#admin-modal-body').html($modalBody.html());
                // Re-parse unobtrusive validation for the new form content
                if ($.validator && $.validator.unobtrusive) {
                    $.validator.unobtrusive.parse('#addAdminModal form');
                }
            }
            // Look for validation errors in the returned HTML
            var hasValidationError = $(html).find('.validation-summary-errors, .field-validation-error, .is-invalid').length > 0;
            if (!hasValidationError) {
                var modal = bootstrap.Modal.getInstance(document.getElementById('addAdminModal'));
                if (modal) modal.hide();
                window.location.reload();
            }
        },
        error: function (xhr, status, error) {
            alert('An error occurred while submitting the form. Please try again.');
        }
    });
});
