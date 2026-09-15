FROM prom/prometheus:v3.7.3 AS prometheus
COPY monitoring/prometheus/prometheus.yml /etc/prometheus/prometheus.yml
COPY monitoring/prometheus/rfid-ops.rules.yml /etc/prometheus/rfid-ops.rules.yml
USER nobody

FROM prom/alertmanager:v0.28.1 AS alertmanager
COPY monitoring/alertmanager/alertmanager.yml /etc/alertmanager/alertmanager.yml
USER nobody

FROM grafana/grafana:12.3.0 AS grafana
COPY monitoring/grafana/provisioning /etc/grafana/provisioning
COPY monitoring/grafana/dashboards /var/lib/grafana/dashboards
USER 472
