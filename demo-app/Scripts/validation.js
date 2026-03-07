// Validation Scripts with Some CSP Violations

(function() {
    'use strict';
    
    // Form validation helper
    window.FormValidator = {
        validateEmail: function(email) {
            var re = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
            return re.test(email);
        },
        
        validatePhone: function(phone) {
            var re = /^[\d\s\-\(\)]+$/;
            return re.test(phone);
        },
        
        validateRequired: function(value) {
            return value && value.trim().length > 0;
        },
        
        // Violation: Using eval for dynamic validation rules
        validateWithRule: function(value, ruleString) {
            try {
                // This is a violation - using eval for dynamic rules
                var rule = eval('(function(value) { return ' + ruleString + '; })');
                return rule(value);
            } catch (e) {
                console.error('Validation rule error:', e);
                return false;
            }
        },
        
        // Violation: Function constructor for custom validators
        createCustomValidator: function(validationCode) {
            return new Function('value', validationCode);
        }
    };
    
    // Real-time validation
    $(document).ready(function() {
        $('input[data-validate]').on('blur', function() {
            var $input = $(this);
            var validator = $input.data('validate');
            var value = $input.val();
            
            if (validator && FormValidator[validator]) {
                var isValid = FormValidator[validator](value);
                if (isValid) {
                    $input.removeClass('is-invalid').addClass('is-valid');
                } else {
                    $input.removeClass('is-valid').addClass('is-invalid');
                }
            }
        });
    });
})();

