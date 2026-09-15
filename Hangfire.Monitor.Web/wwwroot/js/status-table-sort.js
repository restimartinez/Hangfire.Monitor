(function () {
    "use strict";

    var table = document.querySelector(".status-table");
    if (!table) {
        return;
    }

    var tbody = table.tBodies[0];
    if (!tbody) {
        return;
    }

    var textCollator = new Intl.Collator("en", {
        usage: "sort",
        sensitivity: "base",
        numeric: true
    });

    var headers = table.querySelectorAll("thead th[data-sort-type]");

    headers.forEach(function (header) {
        header.addEventListener("click", function () {
            var type = header.getAttribute("data-sort-type");
            var columnIndex = header.cellIndex;
            var ascending = header.getAttribute("aria-sort") !== "ascending";

            headers.forEach(function (otherHeader) {
                otherHeader.setAttribute("aria-sort", "none");
            });
            header.setAttribute("aria-sort", ascending ? "ascending" : "descending");

            var rows = Array.from(tbody.rows);
            rows.sort(function (rowA, rowB) {
                var comparison = compareCells(
                    cellSortValue(rowA.cells[columnIndex]),
                    cellSortValue(rowB.cells[columnIndex]),
                    type);
                if (comparison !== 0) {
                    return ascending ? comparison : -comparison;
                }

                return rowA.sectionRowIndex - rowB.sectionRowIndex;
            });

            rows.forEach(function (row) {
                tbody.appendChild(row);
            });
        });
    });

    function cellSortValue(cell) {
        if (cell.hasAttribute("data-sort-value")) {
            return cell.getAttribute("data-sort-value") || "";
        }

        return (cell.textContent || "").trim();
    }

    function compareCells(left, right, type) {
        if (type === "number") {
            return Number(left) - Number(right);
        }

        if (type === "date") {
            var leftEmpty = left === "";
            var rightEmpty = right === "";
            if (leftEmpty && rightEmpty) {
                return 0;
            }

            if (leftEmpty) {
                return -1;
            }

            if (rightEmpty) {
                return 1;
            }

            if (left < right) {
                return -1;
            }

            if (left > right) {
                return 1;
            }

            return 0;
        }

        return textCollator.compare(left, right);
    }
})();
