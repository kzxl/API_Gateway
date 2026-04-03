# 1. Build environment
FROM node:20-alpine AS build

WORKDIR /app

# Copy package and install dependencies
COPY package.json package-lock.json ./
RUN npm ci

# Copy the rest of the source code
COPY . ./

# Build the Vite application
RUN npm run build

# 2. Production environment
FROM nginx:alpine

# Remove default nginx static assets
RUN rm -rf /usr/share/nginx/html/*

# Copy built assets from build block
COPY --from=build /app/dist /usr/share/nginx/html

# Replace default nginx config with our custom config
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]
