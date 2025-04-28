// loader.js: global loader logic
window.showLoader = function() {
    document.getElementById('global-loader-overlay').classList.add('active');
};
window.hideLoader = function() {
    document.getElementById('global-loader-overlay').classList.remove('active');
};

// Show loader on all AJAX start, hide on stop (for jQuery)
if (window.jQuery) {
    $(document).ajaxStart(function() {
        showLoader();
    });
    $(document).ajaxStop(function() {
        hideLoader();
    });
}

// Optional: Show loader on form submit for forms with data-loader="true"
document.addEventListener('DOMContentLoaded', function() {
    document.querySelectorAll('form[data-loader="true"]').forEach(function(form) {
        form.addEventListener('submit', function() {
            showLoader();
        });
    });
});
