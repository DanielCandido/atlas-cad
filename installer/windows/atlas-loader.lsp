;;; Public release loader. Proprietary STEELFLOW source is distributed only as FAS.
(vl-load-com)

(if (not *ATLAS-RELEASE-LOADED*)
  (progn
    (setq *ATLAS-RELEASE-LOADED* T)
    (setq atlas-loader-file (findfile "atlas-loader.lsp"))
    (if atlas-loader-file
      (progn
        (setq atlas-release-root (vl-filename-directory atlas-loader-file))
        (load (strcat atlas-release-root "\\atlas-bridge.lsp"))
        (load (strcat atlas-release-root "\\STEELFLOW.fas"))
      )
      (princ "\n[ATLAS] Release loader directory was not found.")
    )
  )
)
(princ)
