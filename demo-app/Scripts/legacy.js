// Legacy JavaScript Patterns (for testing legacy mode)

(function() {
    'use strict';
    
    // Simulate legacy patterns
    window.LegacyHelpers = {
        // Violation: eval for legacy data parsing
        parseLegacyData: function(dataString) {
            // Old way of parsing (BAD)
            return eval('(' + dataString + ')');
        },
        
        // Violation: Function constructor for legacy callbacks
        createLegacyCallback: function(callbackCode) {
            return new Function('data', callbackCode);
        },
        
        // Simulate WebResource.axd pattern
        loadWebResource: function(resourceId) {
            var script = document.createElement('script');
            script.src = '/WebResource.axd?d=' + resourceId + '&t=' + new Date().getTime();
            script.onload = function() {
                console.log('WebResource loaded:', resourceId);
            };
            document.head.appendChild(script);
        },
        
        // Legacy ViewState access pattern
        getViewState: function() {
            var viewState = document.getElementById('__VIEWSTATE');
            if (viewState) {
                // Violation: eval for ViewState processing
                try {
                    var data = eval('(' + viewState.value + ')');
                    return data;
                } catch (e) {
                    console.error('Error processing ViewState:', e);
                    return null;
                }
            }
            return null;
        },
        
        // Legacy postback simulation
        doPostBack: function(eventTarget, eventArgument) {
            // Simulate ASP.NET postback
            var form = document.forms[0];
            if (form) {
                var hiddenField = document.createElement('input');
                hiddenField.type = 'hidden';
                hiddenField.name = '__EVENTTARGET';
                hiddenField.value = eventTarget;
                form.appendChild(hiddenField);
                
                if (eventArgument) {
                    var argField = document.createElement('input');
                    argField.type = 'hidden';
                    argField.name = '__EVENTARGUMENT';
                    argField.value = eventArgument;
                    form.appendChild(argField);
                }
                
                form.submit();
            }
        }
    };
})();

