// AJAX Helper Functions with CSP Violations

(function() {
    'use strict';
    
    // Global AJAX configuration
    $.ajaxSetup({
        beforeSend: function(xhr, settings) {
            // Add CSRF token
            if (settings.type === 'POST') {
                var token = $('input[name="__RequestVerificationToken"]').val();
                if (token) {
                    xhr.setRequestHeader('RequestVerificationToken', token);
                }
            }
        }
    });
    
    // Violation: Using eval to parse responses (BAD PRACTICE)
    function parseResponse(responseText) {
        try {
            // Try JSON.parse first (good)
            return JSON.parse(responseText);
        } catch (e) {
            // Fallback to eval (BAD - violation)
            try {
                return eval('(' + responseText + ')');
            } catch (evalError) {
                console.error('Error parsing response:', evalError);
                return null;
            }
        }
    }
    
    // Violation: Function constructor for dynamic handlers
    function createAjaxHandler(successCode, errorCode) {
        var handler = new Function('response', successCode);
        var errorHandler = new Function('error', errorCode);
        
        return {
            success: handler,
            error: errorHandler
        };
    }
    
    // Helper function for AJAX calls
    window.AjaxHelpers = {
        get: function(url, success, error) {
            $.ajax({
                url: url,
                type: 'GET',
                success: function(response) {
                    var parsed = parseResponse(JSON.stringify(response));
                    if (success) success(parsed);
                },
                error: error || function(xhr, status, err) {
                    console.error('AJAX Error:', err);
                }
            });
        },
        
        post: function(url, data, success, error) {
            $.ajax({
                url: url,
                type: 'POST',
                data: data,
                success: function(response) {
                    var parsed = parseResponse(JSON.stringify(response));
                    if (success) success(parsed);
                },
                error: error || function(xhr, status, err) {
                    console.error('AJAX Error:', err);
                }
            });
        },
        
        // Violation: setTimeout with string
        delayedRequest: function(url, delay, callback) {
            setTimeout("AjaxHelpers.get('" + url + "', " + callback.toString() + ");", delay);
        }
    };
})();

