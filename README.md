# SOnB_PRO
Systemy Odporne na Błędy
Projekt gr 1ID21B
Zespół projektowy nr 1
Temat projektu nr 5 Glosowanie Przybliżone

Jak uruchomić:
- zainstaluj dotnet-runtime-7.0.20
- zainstaluj prometheus (używaliśmy 3.3.0.windows-amd64)
- zaktualizuj plik prometheus.yml o :
    '- job_name: "sonb-server"
        static_configs:
          - targets: ["localhost:9100"]
            labels:
              app: "sonb"'
    w sekcji _scrape_configs:_
- uruchom komendę prometheus.exe --config.file=prometheus.yml
- zainstaluj grafanę
- uruchom bin -> grafana-server.exe
- otwórz http://localhost:3000/
- w connection -> data sources dodaj prometheus z connection = http://localhost:9090
- w dashboards -> new -> import zaimportuj plik grafanDashboard.json
- uruchom autostart.bat lub projekt SOnB ( defaultowo 3 razy ) w celu utworzenia serwera i klientów
- dodaj nową wizualizacje i korzystaj z dostępnych metryk np: actual_timestamp_median_value
