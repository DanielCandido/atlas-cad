;;; ATLAS CAD M1 bridge client. This file contains no proprietary STEELFLOW logic.
;;; Points passed to atlas-bridge-draw must already be WCS coordinates.

(vl-load-com)

(defun atlas--replace-all (text old new / found cursor output)
  (setq cursor 0 output "")
  (while (setq found (vl-string-search old text cursor))
    (setq output (strcat output (substr text (1+ cursor) (- found cursor)) new))
    (setq cursor (+ found (strlen old)))
  )
  (strcat output (substr text (1+ cursor)))
)

(defun atlas--json-escape (value / escaped)
  (setq escaped (atlas--replace-all value "\\" "\\\\"))
  (setq escaped (atlas--replace-all escaped "\"" "\\\""))
  (setq escaped (atlas--replace-all escaped (chr 13) "\\r"))
  (setq escaped (atlas--replace-all escaped (chr 10) "\\n"))
  (setq escaped (atlas--replace-all escaped (chr 9) "\\t"))
  escaped
)

(defun atlas--json-string-or-null (value)
  (if value
    (strcat "\"" (atlas--json-escape value) "\"")
    "null"
  )
)

;;; A .NET LispFunction may marshal a one-value ResultBuffer as a LISP list.
;;; Accept that representation as well as a scalar return value.
(defun atlas--bridge-scalar (value)
  (if (and (listp value) (= (length value) 1))
    (car value)
    value
  )
)

(defun atlas--json-real (value)
  (vl-string-translate "," "." (rtos value 2 16))
)

(defun atlas--json-point (point)
  (strcat
    "{\"x\":" (atlas--json-real (car point))
    ",\"y\":" (atlas--json-real (cadr point))
    ",\"z\":" (atlas--json-real (if (caddr point) (caddr point) 0.0))
    "}"
  )
)

(defun atlas--json-points (points / output separator)
  (setq output "[" separator "")
  (foreach point points
    (setq output (strcat output separator (atlas--json-point point)))
    (setq separator ",")
  )
  (strcat output "]")
)

(defun atlas--read-file (path / stream line output)
  (setq stream (open path "r") output "")
  (if stream
    (progn
      (while (setq line (read-line stream))
        (setq output (strcat output line))
      )
      (close stream)
      output
    )
    nil
  )
)

(defun atlas--json-string-value (json key / marker start finish)
  (setq marker (strcat "\"" key "\":\""))
  (setq start (vl-string-search marker json))
  (if start
    (progn
      (setq start (+ start (strlen marker)))
      (setq finish (vl-string-search "\"" json start))
      (if finish (substr json (1+ start) (- finish start)))
    )
  )
)

(defun atlas-bridge-draw
       (points-wcs normal-wcs style-name layer-name closed source-handle
        / request-id root request-temp request-path response-path drawing-id
          stream payload response success handle error-code)
  (setq request-id (atlas--bridge-scalar (ATLAS_NEW_REQUEST_ID)))
  (setq root (atlas--bridge-scalar (ATLAS_BRIDGE_ROOT)))
  (if (not (and request-id root))
    (progn (princ "\n[ATLAS] Plugin bridge is not loaded.") nil)
    (progn
      (setq request-path (strcat root "\\" request-id ".request.json"))
      (setq request-temp (strcat request-path ".tmp"))
      (setq response-path (strcat root "\\" request-id ".response.json"))
      (setq drawing-id (atlas--bridge-scalar (ATLAS_ACTIVE_DRAWING_IDENTITY)))
      (setq payload
        (strcat
          "{\"protocolVersion\":1"
          ",\"requestId\":\"" request-id "\""
          ",\"styleName\":\"" (atlas--json-escape style-name) "\""
          ",\"pointsWcs\":" (atlas--json-points points-wcs)
          ",\"normalWcs\":" (atlas--json-point normal-wcs)
          ",\"layerId\":null"
          ",\"layerName\":\"" (atlas--json-escape layer-name) "\""
          ",\"closed\":" (if closed "true" "false")
          ",\"sourceHandle\":" (atlas--json-string-or-null source-handle)
          ",\"expectedDrawingIdentity\":\"" (atlas--json-escape drawing-id) "\""
          ",\"justification\":1"
          ",\"scale\":1.0}"
        )
      )

      (if (findfile request-temp) (vl-file-delete request-temp))
      (if (findfile response-path) (vl-file-delete response-path))
      (setq stream (open request-temp "w"))
      (if (not stream)
        (progn (princ "\n[ATLAS] Could not create request payload.") nil)
        (progn
          (write-line payload stream)
          (close stream)
          (if (not (vl-file-rename request-temp request-path))
            (progn
              (if (findfile request-temp) (vl-file-delete request-temp))
              (princ "\n[ATLAS] Could not publish request payload.")
              nil
            )
            (progn
              (command "ATLAS_DRAW_MLINE" request-id)
              (setq response (atlas--read-file response-path))
              (if (findfile response-path) (vl-file-delete response-path))
              (setq success (and response (vl-string-search "\"success\":true" response)))
              (if success
                (progn
                  (setq handle (atlas--json-string-value response "newEntityHandle"))
                  (list (cons 'success T) (cons 'handle handle))
                )
                (progn
                  (setq error-code (if response (atlas--json-string-value response "errorCode") "response_missing"))
                  (princ (strcat "\n[ATLAS] Bridge failed: " error-code "."))
                  (list (cons 'success nil) (cons 'error error-code))
                )
              )
            )
          )
        )
      )
    )
  )
)

;;; Ensures one named style from the catalog installed by ATLAS_INSTALL_CATALOG.
;;; Returns ((success . T) (style . name) (status . created|existing)) on success.
(defun atlas-bridge-ensure-style
       (style-name
        / request-id root request-temp request-path response-path drawing-id
          stream payload response success status resolved-name error-code error-message)
  (setq request-id (atlas--bridge-scalar (ATLAS_NEW_REQUEST_ID)))
  (setq root (atlas--bridge-scalar (ATLAS_BRIDGE_ROOT)))
  (if (not (and request-id root))
    (progn (princ "\n[ATLAS] Plugin bridge is not loaded.") nil)
    (progn
      (setq request-path (strcat root "\\" request-id ".request.json"))
      (setq request-temp (strcat request-path ".tmp"))
      (setq response-path (strcat root "\\" request-id ".response.json"))
      (setq drawing-id (atlas--bridge-scalar (ATLAS_ACTIVE_DRAWING_IDENTITY)))
      (setq payload
        (strcat
          "{\"protocolVersion\":1"
          ",\"requestId\":\"" request-id "\""
          ",\"expectedDrawingIdentity\":\"" (atlas--json-escape drawing-id) "\""
          ",\"styleName\":\"" (atlas--json-escape style-name) "\"}"
        )
      )
      (if (findfile request-temp) (vl-file-delete request-temp))
      (if (findfile response-path) (vl-file-delete response-path))
      (setq stream (open request-temp "w"))
      (if (not stream)
        (progn (princ "\n[ATLAS] Could not create style request payload.") nil)
        (progn
          (write-line payload stream)
          (close stream)
          (if (not (vl-file-rename request-temp request-path))
            (progn
              (if (findfile request-temp) (vl-file-delete request-temp))
              (princ "\n[ATLAS] Could not publish style request payload.")
              nil
            )
            (progn
              (command "ATLAS_ENSURE_STYLE" request-id)
              (setq response (atlas--read-file response-path))
              (if (findfile response-path) (vl-file-delete response-path))
              (setq success (and response (vl-string-search "\"success\":true" response)))
              (if success
                (progn
                  (setq status (atlas--json-string-value response "status"))
                  (setq resolved-name (atlas--json-string-value response "styleName"))
                  (list
                    (cons 'success T)
                    (cons 'style resolved-name)
                    (cons 'status status)
                  )
                )
                (progn
                  (setq error-code (if response (atlas--json-string-value response "errorCode") "response_missing"))
                  (setq error-message (if response (atlas--json-string-value response "message") nil))
                  (princ
                    (strcat
                      "\n[ATLAS] Style ensure failed: " error-code
                      (if (and error-message (/= error-message ""))
                        (strcat " (" error-message ")")
                        ""
                      )
                      "."
                    )
                  )
                  (list (cons 'success nil) (cons 'error error-code))
                )
              )
            )
          )
        )
      )
    )
  )
)

(defun c:ATLASBRIDGETEST (/ first second first-wcs second-wcs normal-wcs result)
  (setq first (getpoint "\nFirst point: "))
  (if first (setq second (getpoint first "\nSecond point: ")))
  (if (and first second)
    (progn
      (setq first-wcs (trans first 1 0))
      (setq second-wcs (trans second 1 0))
      (setq normal-wcs (trans '(0.0 0.0 1.0) 2 0 T))
      (setq result
        (atlas-bridge-draw
          (list first-wcs second-wcs)
          normal-wcs
          "ATLAS_TEST_MLINE"
          (getvar "CLAYER")
          nil
          nil
        )
      )
      (if (cdr (assoc 'success result))
        (princ (strcat "\n[ATLAS] Test succeeded. Handle: " (cdr (assoc 'handle result))))
      )
    )
  )
  (princ)
)

(defun c:ATLASBRIDGETESTPATH
       (/ point previous points-wcs closed-answer normal-wcs result)
  (princ "\nSelect at least two path points; press Enter to finish.")
  (while
    (setq point
      (if previous
        (getpoint previous "\nNext point <finish>: ")
        (getpoint "\nFirst point: ")
      )
    )
    (setq points-wcs (append points-wcs (list (trans point 1 0))))
    (setq previous point)
  )
  (if (< (length points-wcs) 2)
    (princ "\n[ATLAS] At least two points are required.")
    (progn
      (initget "Yes No")
      (setq closed-answer (getkword "\nClosed path? [Yes/No] <No>: "))
      (setq normal-wcs (trans '(0.0 0.0 1.0) 2 0 T))
      (setq result
        (atlas-bridge-draw
          points-wcs
          normal-wcs
          "ATLAS_TEST_MLINE"
          (getvar "CLAYER")
          (= closed-answer "Yes")
          nil
        )
      )
      (if (cdr (assoc 'success result))
        (princ (strcat "\n[ATLAS] Path test succeeded. Handle: " (cdr (assoc 'handle result))))
      )
    )
  )
  (princ)
)

(defun c:ATLASSTYLETEST
       (/ request-id root request-temp request-path response-path drawing-id
          stream payload response success status style-name error-code)
  (setq request-id (atlas--bridge-scalar (ATLAS_NEW_REQUEST_ID)))
  (setq root (atlas--bridge-scalar (ATLAS_BRIDGE_ROOT)))
  (if (not (and request-id root))
    (princ "\n[ATLAS] Plugin bridge is not loaded.")
    (progn
      (setq request-path (strcat root "\\" request-id ".request.json"))
      (setq request-temp (strcat request-path ".tmp"))
      (setq response-path (strcat root "\\" request-id ".response.json"))
      (setq drawing-id (atlas--bridge-scalar (ATLAS_ACTIVE_DRAWING_IDENTITY)))
      (setq payload
        (strcat
          "{\"protocolVersion\":1"
          ",\"requestId\":\"" request-id "\""
          ",\"expectedDrawingIdentity\":\"" (atlas--json-escape drawing-id) "\""
          ",\"style\":{"
          "\"name\":\"ATLAS_M2_TEST_STYLE\""
          ",\"description\":\"Public synthetic M2 validation style\""
          ",\"flags\":275"
          ",\"fillColorIndex\":3"
          ",\"startAngleDegrees\":90.0"
          ",\"endAngleDegrees\":90.0"
          ",\"elements\":["
          "{\"offset\":10.0,\"colorIndex\":1,\"linetypeName\":\"CONTINUOUS\"},"
          "{\"offset\":0.0,\"colorIndex\":2,\"linetypeName\":\"CONTINUOUS\"},"
          "{\"offset\":-10.0,\"colorIndex\":3,\"linetypeName\":\"CONTINUOUS\"}"
          "]}}"
        )
      )
      (if (findfile request-temp) (vl-file-delete request-temp))
      (if (findfile response-path) (vl-file-delete response-path))
      (setq stream (open request-temp "w"))
      (if (not stream)
        (princ "\n[ATLAS] Could not create style request payload.")
        (progn
          (write-line payload stream)
          (close stream)
          (if (not (vl-file-rename request-temp request-path))
            (progn
              (if (findfile request-temp) (vl-file-delete request-temp))
              (princ "\n[ATLAS] Could not publish style request payload.")
            )
            (progn
              (command "ATLAS_ENSURE_STYLE" request-id)
              (setq response (atlas--read-file response-path))
              (if (findfile response-path) (vl-file-delete response-path))
              (setq success (and response (vl-string-search "\"success\":true" response)))
              (if success
                (progn
                  (setq status (atlas--json-string-value response "status"))
                  (setq style-name (atlas--json-string-value response "styleName"))
                  (princ (strcat "\n[ATLAS] Style test succeeded: " style-name " (" status ")."))
                )
                (progn
                  (setq error-code (if response (atlas--json-string-value response "errorCode") "response_missing"))
                  (princ (strcat "\n[ATLAS] Style test failed: " error-code "."))
                )
              )
            )
          )
        )
      )
    )
  )
  (princ)
)

(defun c:ATLASCATALOGTEST (/ styles style-name result status)
  (if (not (member "ATLAS_CATALOG_STYLE_NAMES" (atoms-family 1)))
    (princ "\n[ATLAS] Catalog bridge is not loaded.")
    (progn
      (setq styles (ATLAS_CATALOG_STYLE_NAMES))
      (setq style-name
        (cond
          ((= (type styles) 'STR) styles)
          ((and (listp styles) styles) (car styles))
          (T nil)
        )
      )
      (if (not style-name)
        (princ "\n[ATLAS] Installed catalog has no available styles.")
        (progn
          (setq result (atlas-bridge-ensure-style style-name))
          (if (cdr (assoc 'success result))
            (progn
              (setq status (cdr (assoc 'status result)))
              (princ (strcat "\n[ATLAS] Catalog test succeeded: " style-name " (" status ")."))
            )
            (princ "\n[ATLAS] Catalog test failed; no existing style was overwritten.")
          )
        )
      )
    )
  )
  (princ)
)

(princ "\nATLAS bridge client loaded. Commands: ATLASBRIDGETEST, ATLASBRIDGETESTPATH, ATLASSTYLETEST, ATLASCATALOGTEST, ATLAS_INSTALL_CATALOG, ATLAS_LICENSE_STATUS, ATLAS_LICENSE_ACTIVATE, ATLAS_LICENSE_RENEW, ATLAS_DIAGNOSTICS.")
(princ)
