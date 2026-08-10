(function () {
  "use strict";

  const board = document.getElementById("board");
  if (!board) return;

  function token() {
    const el = document.querySelector('input[name="__RequestVerificationToken"]');
    return el ? el.value : "";
  }

  function orderedIds(list) {
    return Array.from(list.querySelectorAll(".card"))
      .map(c => parseInt(c.dataset.cardId, 10));
  }

  async function move(cardId, targetStatus, ids, note) {
    try {
      const response = await fetch("/?handler=Move", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "RequestVerificationToken": token()
        },
        body: JSON.stringify({
          cardId: cardId,
          targetStatus: targetStatus,
          orderedCardIds: ids,
          note: note,
          projectId: (function () {
            var m = window.location.search.match(/[?&]ProjectId=(\d+)/);
            return m ? parseInt(m[1], 10) : null;
          })()
        })
      });

      if (!response.ok) {
        const errorText = await response.text();
        alert(errorText);
        window.location.reload();
        return;
      }

      board.innerHTML = await response.text();
      wire();
    } catch (err) {
      console.error("Move failed", err);
      window.location.reload();
    }
  }

  function onDrop(evt) {
    const targetList = evt.to;
    const targetStatus = targetList.dataset.status;
    const cardId = parseInt(evt.item.dataset.cardId, 10);
    const ids = orderedIds(targetList);

    let note = null;
    const fromStatus = evt.from.dataset.status;
    if (fromStatus === "Review" && targetStatus === "Ready") {
      note = window.prompt("What should the agent do differently?");
      if (note === null || note.trim() === "") {
        window.location.reload();
        return;
      }
    }

    move(cardId, targetStatus, ids, note);
  }

  var sortables = [];

  function wire() {
    sortables.forEach(function (s) {
      try { s.destroy(); } catch (e) { /* ignore */ }
    });
    sortables = [];

    board.querySelectorAll(".cards").forEach(function (list) {
      // In Progress belongs to the Runner. Cards there are not draggable and
      // nothing may be dropped into it. The server enforces this too.
      const locked = list.dataset.status === "InProgress";
      sortables.push(Sortable.create(list, {
        group: { name: "board", pull: !locked, put: !locked },
        animation: 120,
        sort: !locked,
        ghostClass: "dragging",
        onEnd: onDrop
      }));
    });
  }

  wire();

  board.addEventListener("htmx:afterSettle", wire);
})();