SHELL := /usr/bin/env bash

ENV_FILE ?= .env
BUILDER ?= lumirise-builder

REGISTRY ?= $(shell set -a; . $(ENV_FILE); set +a; echo $$REGISTRY)
APP_VERSION ?= $(shell set -a; . $(ENV_FILE); set +a; echo $$APP_VERSION)

API_IMAGE := $(REGISTRY)/lumi-rise
UI_IMAGE := $(REGISTRY)/lumi-rise-ui

LOCAL_COMPOSE := docker compose -f docker-compose.yml

.DEFAULT_GOAL := help

.PHONY: help
help:
	@echo "Targets:"
	@echo "  make local-build     Build local amd64 services via docker-compose.yml"
	@echo "  make local-up        Start local amd64 stack via docker-compose.yml"
	@echo "  make local-down      Stop local stack"
	@echo "  make release         Build+push amd64 and arm64 images, then create multi-arch manifests"
	@echo "  make release-api     Build+push api images for amd64 and arm64"
	@echo "  make release-ui      Build+push ui images for amd64 and arm64"
	@echo "  make release-manifest Create/refresh multi-arch manifest tags"

.PHONY: local-build
local-build:
	$(LOCAL_COMPOSE) build

.PHONY: local-up
local-up:
	$(LOCAL_COMPOSE) up -d --build

.PHONY: local-down
local-down:
	$(LOCAL_COMPOSE) down

.PHONY: ensure-builder
ensure-builder:
	@if ! docker buildx inspect $(BUILDER) >/dev/null 2>&1; then \
		docker buildx create --name $(BUILDER) --driver docker-container --use; \
	fi
	docker buildx use $(BUILDER)
	docker buildx inspect --bootstrap >/dev/null

.PHONY: release
release: ensure-builder release-api release-ui release-manifest

.PHONY: release-api
release-api:
	docker buildx build --builder $(BUILDER) --platform linux/amd64 -f src/LumiRise.Api/Dockerfile -t $(API_IMAGE):$(APP_VERSION)-amd64 --push src
	docker buildx build --builder $(BUILDER) --platform linux/arm64 -f src/LumiRise.Api/Dockerfile -t $(API_IMAGE):$(APP_VERSION)-arm64 --push src

.PHONY: release-ui
release-ui:
	docker buildx build --builder $(BUILDER) --platform linux/amd64 -f src/LumiRise.Ui/Dockerfile -t $(UI_IMAGE):$(APP_VERSION)-amd64 --push src/LumiRise.Ui
	docker buildx build --builder $(BUILDER) --platform linux/arm64 -f src/LumiRise.Ui/Dockerfile -t $(UI_IMAGE):$(APP_VERSION)-arm64 --push src/LumiRise.Ui

.PHONY: release-manifest
release-manifest:
	docker buildx imagetools create -t $(API_IMAGE):$(APP_VERSION) $(API_IMAGE):$(APP_VERSION)-amd64 $(API_IMAGE):$(APP_VERSION)-arm64
	docker buildx imagetools create -t $(UI_IMAGE):$(APP_VERSION) $(UI_IMAGE):$(APP_VERSION)-amd64 $(UI_IMAGE):$(APP_VERSION)-arm64
