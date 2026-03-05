async function callApi(url, body, resultId, btnElement) {
    const user = getActiveUser();
    const resultDiv = document.getElementById(resultId);
    const originalText = btnElement.innerText;

    btnElement.disabled = true;
    btnElement.innerHTML = `<span class="spinner-border spinner-border-sm"></span> Processing...`;
    resultDiv.style.display = "none";

    try {
        const response = await fetch(url, {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                "X-User-Account": user //  Aa header "headers" object ni andar hovo joiye
            },
            body: JSON.stringify(body)
        });

        const text = await response.text();
        resultDiv.innerText = text;
        resultDiv.style.display = "block";

        // Success/Error styling
        if (response.ok) {
            resultDiv.style.borderLeftColor = "#22c55e";
            resultDiv.style.backgroundColor = "#f0fdf4";
        } else {
            resultDiv.style.borderLeftColor = "#ef4444";
            resultDiv.style.backgroundColor = "#fef2f2";
        }

    } catch (error) {
        resultDiv.innerText = "Network Error: " + error.message;
        resultDiv.style.display = "block";
        resultDiv.style.borderLeftColor = "#ef4444";
    } finally {
        btnElement.disabled = false;
        btnElement.innerText = originalText;
    }
}
// Aa function global dropdown mathi active user ni value aapshe
function getActiveUser() {
    return document.getElementById("globalSelectedUser").value;
}

async function runOrderSnapshot(event) {
    const btn = event.target;
    const user = getActiveUser();
    const valid = parseInt(document.getElementById("validOrders").value) || 0;
    const location = document.getElementById("orderLocation").value;
    const invalid = parseInt(document.getElementById("invalidOrders").value) || 0;

    const data = {
        userAccount: user,
        validOrders: valid,
        invalidOrders: invalid,
        location: location
    };

    // UI Feedback
    btn.disabled = true;
    btn.innerText = "Processing...";

    try {
        const confirm = await Swal.fire({
            title: 'Are you sure?',
            text: `You are about to create ${valid + invalid} orders.`,
            icon: 'question',
            showCancelButton: true,
            confirmButtonText: 'Yes, Create them!'
        });

        if (!confirm.isConfirmed) return; // Jo user cancel kare to karshe nahi
        const response = await fetch("/api/ordersnapshot/run", {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'X-User-Account': user // 👈 Header jaroori che
            },
            body: JSON.stringify(data)
        });

        const result = await response.json();

        if (response.ok) {
            Swal.fire({
                title: 'Scenario Completed',
                html: `Valid: ${result.validCount} | Invalid: ${result.invalidCount}<br>${result.message}`,
                icon: 'success',
                confirmButtonText: 'OK'
            });
        } else {
            // ❌ Error Popup
            Swal.fire('Failed', result.message || 'Something went wrong', 'error');
        }
    } catch (error) {
        Swal.fire('Connection Error', 'Server is not responding', 'error');
    } finally {
        btn.disabled = false;
        btn.innerText = "Run Order Snapshot";
    }
}
document.addEventListener("DOMContentLoaded", function () {
    loadLinnworksLocations();      // Order Snapshot
    loadScenarioLocations();       // 👈 ADD THIS

    const userDropdown = document.getElementById("globalSelectedUser");
    if (userDropdown) {
        userDropdown.addEventListener("change", function () {
            console.log("User changed, refreshing locations...");
            loadLinnworksLocations();
            loadScenarioLocations();   // 👈 ADD THIS
        });
    }
});

async function loadLinnworksLocations() {
    const selectedUser = getActiveUser(); 
    const locationDropdown = document.getElementById("orderLocation");

    if (!locationDropdown) return;

    locationDropdown.innerHTML = '<option>Loading...</option>';

    try {
        const response = await fetch(`/api/ordersnapshot/locations?userAccount=${selectedUser}`, {
            method: 'GET',
            headers: {
                'X-User-Account': selectedUser 
            }
        });
        const locations = await response.json();

        if (response.ok) {
            locationDropdown.innerHTML = "";
            locations.forEach(loc => {
                let option = document.createElement("option");
                option.value = loc;
                option.text = loc;
                locationDropdown.add(option);
            });
        }
    } catch (error) {
        console.error("Error refreshing locations:", error);
    }
}
async function loadScenarioLocations() {
    const selectedUser = getActiveUser();
    const dropdown = document.getElementById("orderLocation2");

    if (!dropdown) return;

    dropdown.innerHTML = '<option>Loading...</option>';

    try {
        const response = await fetch(`/api/ordersnapshot/locations?userAccount=${selectedUser}`, {
            method: 'GET',
            headers: { 'X-User-Account': selectedUser }
        });

        const locations = await response.json();

        if (response.ok) {
            dropdown.innerHTML = "";

            locations.forEach(loc => {
                let option = document.createElement("option");
                option.value = loc;
                option.text = loc;
                dropdown.add(option);
            });
        } else {
            dropdown.innerHTML = '<option>Error loading</option>';
        }

    } catch (error) {
        console.error("Scenario location load error:", error);
        dropdown.innerHTML = '<option>Error loading</option>';
    }
}
async function runScenario(event) {
    const btn = event.target;
    const user = getActiveUser();
    const scenarioName = document.getElementById("scenarioName").value;
    const isCommitted = document.getElementById("commitFlag").checked;
    const location = document.getElementById("orderLocation2").value;

    const data = {
        userAccount: user,
        scenario: scenarioName,
        commit: isCommitted,
        location: location
    };

    // Button status change
    btn.disabled = true;
    btn.innerText = "Running...";

    try {
        const response = await fetch("/api/scenario/run", {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'X-User-Account': user // 👈 Header
            },
            body: JSON.stringify(data)
        });

        const result = await response.json();

        if (response.ok) {
            // ✅ Success Popup
            Swal.fire({
                title: 'Scenario Completed',
                html: `<b>Selected:</b> ${result.scenarioName}<br>` +
                    `<b>Commit Changes:</b> ${result.isCommitted ? "Yes" : "No"}<br><br>` +
                    `${result.message}`,
                icon: 'success',
                confirmButtonColor: '#8b5cf6' // Purple color tamara button mujab
            });
        } else {
            // ❌ Error Popup
            Swal.fire('Error', result.message || 'Execution failed', 'error');
        }
    } catch (error) {
        Swal.fire('Connection Error', 'Server busy or offline', 'error');
    } finally {
        btn.disabled = false;
        btn.innerText = "Execute Scenario";
    }
}
// 3. Fetch Full Stock Snapshot Function
async function fetchFullStock() {
    const selectedUser = getActiveUser();
    const btn = event.target;

    // UI Setup
    btn.disabled = true;
    let progress = 0;

    // Real-time progress simulation popup
    Swal.fire({
        title: 'Inventory Sync in Progress',
        html: `Fetching 22,165 items from <b>222 pages</b>... <br><br> 
               <div class="swal-progress-bg">
                    <div id="swal-progress-bar" style="width: 0%; height: 10px; background: linear-gradient(90deg, #0081ff, #9c27b0); border-radius: 5px;"></div>
               </div>
               <small id="page-counter">Page: 1/222</small>`,
        allowOutsideClick: false,
        didOpen: () => {
            Swal.showLoading();
            // Simulate progress for UI beauty
            const bar = document.getElementById('swal-progress-bar');
            const counter = document.getElementById('page-counter');
            let p = 0;
            const interval = setInterval(() => {
                p += 2;
                if (p <= 95) { // 95% sudhi simulate karo, baki API response par
                    bar.style.width = p + "%";
                    counter.innerText = `Page: ${Math.round((p / 100) * 222)}/222`;
                } else { clearInterval(interval); }
            }, 50);
        }
    });

    try {
        const response = await fetch("/api/stocksnapshot/run", {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'X-User-Account': selectedUser },
            body: JSON.stringify({ userAccount: selectedUser })
        });

        const data = await response.json();
        if (response.ok) {
            Swal.fire({
                title: 'Success!',
                text: 'Stock Snapshot for ' + selectedUser + ' generated.',
                icon: 'success',
                confirmButtonColor: '#0081ff'
            });
        }
    } catch (error) {
        Swal.fire('Error', 'Connection Failed', 'error');
    } finally {
        btn.disabled = false;
    }
}

// 4. Auto PO & Order Processing Function
async function runAutoPO() {
    const selectedUser = getActiveUser(); 
    const btn = event.target;
    const apiUrl = "/api/autopo/run";

    const confirmResult = await Swal.fire({
        title: 'Confirm Auto PO',
        text: "Are you sure you want to start PO generation?",
        icon: 'warning',
        showCancelButton: true,
        confirmButtonColor: '#f59e0b',
        confirmButtonText: 'Yes, Run it!'
    });

    if (confirmResult.isConfirmed) {
        btn.disabled = true;
        btn.innerText = "Processing...";

        try {
            const response = await fetch(apiUrl, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'X-User-Account': selectedUser // 👈 Aa header Program.cs mate
                },
                body: JSON.stringify({ userAccount: selectedUser }) // 👈 Payload
            });
            const data = await response.json();

            // Response handle karti vakhte:
            if (response.ok) {
                Swal.fire({
                    title: 'Process Completed!',
                    html: `<b>📦 Orders Processed:</b> ${data.orderCount}<br>` +
                        `<b>📄 POs Created:</b> ${data.poCount}`,
                    icon: 'success'
                });
            }
            else {
                Swal.fire('Error', data.message, 'error');
            }
        } catch (error) {
            Swal.fire('Error', 'Server connection failed', 'error');
        } finally {
            btn.disabled = false;
            btn.innerText = "Run Auto Process";
        }
    }
}
// 5. WeighWise Order Splitter Function
async function executeWeightSplit() {
    const selectedUser = getActiveUser(); 
    const btn = event.target;
    const rawInput = document.getElementById("splitOrderIds").value;
    const maxKg = document.getElementById("splitMaxKg").value;

    const orderNumArray = rawInput.split(',')
        .map(id => parseInt(id.trim()))
        .filter(id => !isNaN(id));

    if (orderNumArray.length === 0) {
        Swal.fire('Input Error', 'Please enter at least one Order ID.', 'warning');
        return;
    }

    // 1. Loading Spinner Start
    Swal.fire({
        title: 'Weight Splitting...',
        text: 'Checking weights and splitting parcels, please wait...',
        allowOutsideClick: false,
        didOpen: () => { Swal.showLoading(); }
    });

    try {
        const response = await fetch("/api/weightsplit/run", {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'X-User-Account': selectedUser },
            body: JSON.stringify({ numOrderIds: orderNumArray, MaxAllowedKg: parseFloat(maxKg) })
        });

        const data = await response.json();

        if (response.ok) {
            // 2. Success/Info Popup (Aa spinner ne bandh kari dese)
            Swal.fire({
                title: data.processedOrders > 0 ? 'Success!' : 'No Action Needed',
                html: `<b>${data.message}</b><br><br>Orders Split: ${data.processedOrders}<br>Limit: ${maxKg}kg`,
                icon: data.processedOrders > 0 ? 'success' : 'info',
                confirmButtonColor: '#8b5cf6'
            });
        } else {
            Swal.fire('Error', data.message || 'Server error', 'error');
        }
    } catch (err) {
        Swal.fire('Connection Error', 'Could not connect to the server.', 'error');
    }
}
async function executeQuantitySplit() {
    const selectedUser = getActiveUser(); 
    const btn = event.target;
    const orderInput = document.getElementById("qtyOrderIds").value;
    const thresholdInput = document.getElementById("qtyThreshold").value;

    const numOrderIds = orderInput.split(',')
        .map(id => parseInt(id.trim()))
        .filter(id => !isNaN(id));

    if (numOrderIds.length === 0) {
        Swal.fire('Input Error', 'Please enter at least one order number.', 'warning');
        return;
    }

    const payload = {
        NumOrderIds: numOrderIds,
        QuantityThreshold: parseInt(thresholdInput)
    };

    // 1. 🔄 Real-time Loading Popup (No Timer)
    Swal.fire({
        title: 'Splitting Orders...',
        text: 'Connecting to server and processing parcels, please wait...',
        allowOutsideClick: false,
        didOpen: () => {
            Swal.showLoading(); // Continuous spinner until response
        }
    });

    btn.disabled = true;

    try {
        const response = await fetch('/api/quantity-split/run', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'X-User-Account': selectedUser },
            body: JSON.stringify(payload)
        });

        const data = await response.json();

        if (response.ok) {
            // Spinner ne bandh karva mate nava Swal.fire call karvo jaroori che
            if (data.processedOrders === 0) {
                // CASE 1: Jo split na thayu hoy
                Swal.fire({
                    title: 'No Splitting Required',
                    html: `<div style="text-align: center;">
                            ✅ <b>All orders are already within the limit.</b><br><br>
                            Orders Processed: ${data.processedOrders}<br>
                            Threshold: ${data.thresholdUsed || payload.QuantityThreshold} 
                           </div>`,
                    icon: 'info',
                    confirmButtonColor: '#3b82f6'
                });
            } else {
                // CASE 2: 🔥 AA BLOCK LAKHVO JAROORI CHE (Success case)
                // Aa code spinner ne stop karse ane success message batavshe
                Swal.fire({
                    title: 'Split Successful!',
                    html: `<div style="text-align: center;">
                            🎉 <b>${data.message}</b><br><br>
                            Total Orders Split: <b>${data.processedOrders}</b><br>
                            Threshold Used: ${data.thresholdUsed || payload.QuantityThreshold}
                           </div>`,
                    icon: 'success',
                    confirmButtonColor: '#8b5cf6'
                });
            }
        } else {
            // ❌ Server Error Popup
            Swal.fire('Error', data.message || 'Unknown error', 'error');
        }
    } catch (err) {
        // 4. ❌ Network Error
        Swal.fire('Connection Error', 'Could not connect to the server.', 'error');
    } finally {
        btn.disabled = false;
    }
}