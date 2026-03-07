// Custom JavaScript file with CSP violations
// This file contains eval() and Function() usage

(function() {
    'use strict';
    
    // Violation: eval() usage for JSON parsing (bad practice)
    function parseJsonUnsafe(jsonString) {
        try {
            return eval('(' + jsonString + ')');
        } catch (e) {
            console.error('Error parsing JSON:', e);
            return null;
        }
    }
    
    // Violation: Function() constructor
    function createDynamicFunction(operation) {
        var func = new Function('x', 'y', 'return x ' + operation + ' y');
        return func;
    }
    
    // Example usage
    var addFunc = createDynamicFunction('+');
    var multiplyFunc = createDynamicFunction('*');
    
    console.log('Add function result:', addFunc(5, 3));
    console.log('Multiply function result:', multiplyFunc(4, 7));
    
    // Violation: setTimeout with string (implicit eval)
    function delayedExecution(code) {
        setTimeout(code, 1000);
    }
    
    // Violation: setInterval with string
    function repeatedExecution(code) {
        setInterval(code, 2000);
    }
    
    // jQuery ready with inline logic (this is OK, but the eval/Function above are violations)
    $(document).ready(function() {
        console.log('Custom script loaded');
        
        // Example: Process data from server (using eval - BAD)
        var serverData = '{"items": [{"id": 1, "name": "Item 1"}]}';
        var data = parseJsonUnsafe(serverData);
        console.log('Parsed data:', data);
    });
    
    // Export functions (if using module pattern)
    window.CustomScripts = {
        parseJsonUnsafe: parseJsonUnsafe,
        createDynamicFunction: createDynamicFunction,
        delayedExecution: delayedExecution,
        repeatedExecution: repeatedExecution
    };
})();

