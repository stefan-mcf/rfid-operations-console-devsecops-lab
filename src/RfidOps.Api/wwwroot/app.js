const elements = {
  registeredTags: document.querySelector("#registered-tags"),
  accessEvents: document.querySelector("#access-events"),
  grantedEvents: document.querySelector("#granted-events"),
  deniedEvents: document.querySelector("#denied-events"),
  pendingOutbox: document.querySelector("#pending-outbox"),
  eventRows: document.querySelector("#event-rows"),
  refreshButton: document.querySelector("#refresh-button"),
  tagForm: document.querySelector("#tag-form"),
  eventForm: document.querySelector("#event-form"),
  commandResult: document.querySelector("#command-result"),
};

function escapeHtml(value) {
  return String(value)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#039;");
}

async function getJson(path, options = {}) {
  const response = await fetch(path, options);
  const contentType = response.headers.get("content-type") || "";
  const body = contentType.includes("application/json")
    ? await response.json()
    : null;

  if (!response.ok) {
    throw new Error(body?.error || `Request failed with status ${response.status}.`);
  }

  return body;
}

async function refreshDashboard() {
  elements.refreshButton.disabled = true;
  try {
    const [status, events] = await Promise.all([
      getJson("/api/v1/status"),
      getJson("/api/v1/events?limit=25"),
    ]);

    elements.registeredTags.textContent = status.registeredTags;
    elements.accessEvents.textContent = status.accessEvents;
    elements.grantedEvents.textContent = status.grantedEvents;
    elements.deniedEvents.textContent = status.deniedEvents;
    elements.pendingOutbox.textContent = status.pendingOutbox;
    renderEvents(events);
  } catch (error) {
    elements.eventRows.innerHTML = `<tr><td colspan="6" class="empty-state">${escapeHtml(error.message)}</td></tr>`;
  } finally {
    elements.refreshButton.disabled = false;
  }
}

function renderEvents(events) {
  if (!events.length) {
    elements.eventRows.innerHTML = '<tr><td colspan="6" class="empty-state">No synthetic reader events have been processed.</td></tr>';
    return;
  }

  elements.eventRows.innerHTML = events.map((event) => {
    const outcomeClass = event.outcome === "Granted" ? "badge--granted" : "badge--denied";
    const outboxClass = event.pendingDelivery ? "badge--pending" : "badge--sent";
    return `<tr>
      <td>${escapeHtml(new Date(event.occurredAtUtc).toLocaleTimeString())}</td>
      <td>${escapeHtml(event.tagId)}</td>
      <td>${escapeHtml(event.readerId)}</td>
      <td><span class="badge ${outcomeClass}">${escapeHtml(event.outcome)}</span></td>
      <td>${escapeHtml(event.reason)}</td>
      <td><span class="badge ${outboxClass}">${event.pendingDelivery ? "Pending" : "Acknowledged"}</span></td>
    </tr>`;
  }).join("");
}

function showResult(message, type) {
  elements.commandResult.textContent = message;
  elements.commandResult.className = `command-result command-result--${type}`;
}

elements.tagForm.addEventListener("submit", async (event) => {
  event.preventDefault();
  const form = new FormData(elements.tagForm);
  try {
    const tagId = String(form.get("tagId"));
    await getJson(`/api/v1/tags/${encodeURIComponent(tagId)}`, {
      method: "PUT",
      headers: {
        "Content-Type": "application/json",
        "X-Admin-Key": String(form.get("adminKey")),
      },
      body: JSON.stringify({
        displayName: form.get("displayName"),
        state: form.get("state"),
      }),
    });
    showResult(`Saved ${tagId.toUpperCase()} as synthetic test data.`, "success");
    await refreshDashboard();
  } catch (error) {
    showResult(error.message, "error");
  }
});

elements.eventForm.addEventListener("submit", async (event) => {
  event.preventDefault();
  const form = new FormData(elements.eventForm);
  try {
    const decision = await getJson("/api/v1/events", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "X-Reader-Key": String(form.get("readerKey")),
      },
      body: JSON.stringify({
        tagId: form.get("tagId"),
        readerId: form.get("readerId"),
      }),
    });
    showResult(`${decision.outcome}: ${decision.reason}. Event ${decision.eventId}.`, "success");
    await refreshDashboard();
  } catch (error) {
    showResult(error.message, "error");
  }
});

elements.refreshButton.addEventListener("click", refreshDashboard);
refreshDashboard();
setInterval(refreshDashboard, 15000);
