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
        note: note
      })
    });

    if (!response.ok) {
      alert(await response.text());
    }

    // Re-render from the server either way. On success it shows the new state;
    // on rejection it snaps the card back to where the server says it belongs.
    board.innerHTML = response.ok
      ? await response.text()
      : (await (await fetch(window.location.href)).text())
          .split('<div class="board" id="board">')[1]
          .split("</div>\n\n")[0];

    wire();
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

  function wire() {
    board.querySelectorAll(".cards").forEach(function (list) {
      // In Progress belongs to the Runner. Cards there are not draggable and
      // nothing may be dropped into it. The server enforces this too.
      const locked = list.dataset.status === "InProgress";
      Sortable.create(list, {
        group: { name: "board", pull: !locked, put: !locked },
        animation: 120,
        sort: !locked,
        ghostClass: "dragging",
        onEnd: onDrop
      });
    });
  }

  wire();
})();